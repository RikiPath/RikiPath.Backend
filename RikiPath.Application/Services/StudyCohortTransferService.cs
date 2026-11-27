using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

// Chuyển lớp có kiểm soát: Học viên gửi yêu cầu -> Admin xem xét -> Duyệt/Từ chối -> cập nhật ghi danh -> thông báo.
// Học viên không bao giờ tự đổi lớp/Mentor; điểm đánh giá Mentor thấp không tự tạo yêu cầu.
public sealed partial class StudyCourseService
{
    public const string CohortNotificationEvent = "CohortLearningNotification";

    internal sealed record TransferCheck(string Code, bool Passed, string Message, int Status);

    private sealed record TransferEvaluation(StudyCohort? Target, MentorGroupPolicy Policy, List<TransferCheck> Checks)
    {
        public bool Eligible => Checks.All(c => c.Passed);
        public TransferCheck? FirstFailure => Checks.FirstOrDefault(c => !c.Passed);
    }

    private static readonly HashSet<string> AllowedEvidenceTypes = ["application/pdf", "image/png", "image/jpeg"];

    private void AddTransferEvent(CohortTransferRequest request, string action, string detail)
        => request.History.Add(new CohortTransferRequestEvent
        {
            Action = action, ActorId = Actor.Id, ActorRole = Actor.Role.ToString(),
            Detail = detail.Length > 2000 ? detail[..2000] : detail, AtUtc = DateTime.UtcNow
        });

    private void QueueNotifications(string eventKey, string aggregateType, string aggregateId,
        IEnumerable<(int UserId, string Title, string Message, string Type, string? LinkUrl)> items)
    {
        var list = items.Where(x => x.UserId > 0).DistinctBy(x => x.UserId).ToList();
        if (list.Count == 0) return;
        // Outbox ghi cùng giao dịch; dispatcher gửi SAU khi commit và tự thử lại khi gửi lỗi.
        uow.AddMentorOutboxEvent(new MentorOutboxEvent
        {
            EventKey = eventKey, AggregateType = aggregateType, AggregateId = aggregateId,
            EventType = CohortNotificationEvent, EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new
            {
                Notifications = list.Select(x => new { x.UserId, x.Title, x.Message, x.Type, x.LinkUrl })
            }),
            CreatedAtUtc = DateTime.UtcNow, AvailableAtUtc = DateTime.UtcNow
        });
    }

    private async Task<MentorGroupPolicy> CurrentPolicyAsync(CancellationToken ct)
    {
        if (groupPolicies is null)
            return new MentorGroupPolicy { Version = 0 };
        var effective = await groupPolicies.GetEffectiveAsync(DateTime.UtcNow, ct);
        Rule(effective is not null, "GROUP_POLICY_UNAVAILABLE",
            "Hệ thống chưa có chính sách lớp học nhóm đang có hiệu lực. Vui lòng liên hệ Quản trị viên.",
            HttpStatusCode.ServiceUnavailable);
        return effective!;
    }

    private static DateTime? FirstSessionStartUtc(StudyCohort cohort)
        => cohort.Sessions.Count == 0 ? null : cohort.Sessions.Min(s => s.Availability.StartTime);

    private const string SourceStartedMessage = "Lớp hiện tại đã bắt đầu; không thể chuyển lớp sau khi khóa học đã bắt đầu.";

    private static bool SourceStarted(StudyCohort sourceCohort, DateTime now)
        => (FirstSessionStartUtc(sourceCohort) ?? sourceCohort.StartsAt) <= now;

    private IQueryable<StudyCohort> CohortForTransfer =>uow.StudyCohorts.Query()
        .Include(x => x.Plan).ThenInclude(x => x.CertificateLevel)
        .Include(x => x.Mentor)
        .Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
        .Include(x => x.Enrollments).AsSplitQuery();

    // Toàn bộ điều kiện chuyển lớp; được gọi khi xem trước và gọi lại trong giao dịch khi duyệt.
    private async Task<TransferEvaluation> EvaluateTransferAsync(StudyCourse source, StudyCohort sourceCohort,
        int? targetCohortId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var policy = await CurrentPolicyAsync(ct);
        var checks = new List<TransferCheck>();
        void Check(string code, bool passed, string message, HttpStatusCode status = HttpStatusCode.UnprocessableEntity)
            => checks.Add(new TransferCheck(code, passed, message, (int)status));

        Check("TRANSFER_DISABLED_BY_POLICY", policy.LearnerTransferEnabled,
            "Chính sách hiện hành chưa cho phép chuyển lớp.");
        Check("TRANSFER_SOURCE_NOT_ACTIVE", source.CohortId == sourceCohort.Id && source.Status == "Active" && source.PaymentStatus == "Paid",
            "Ghi danh nguồn không còn hoạt động hoặc chưa thanh toán.", HttpStatusCode.Conflict);
        Check("TRANSFER_RETAKE_UNSUPPORTED", source.RetakeOfId is null,
            "Ghi danh học lại miễn phí chưa có chính sách chuyển lớp.");
        // Quyết định chính thức của Product Owner: KHÔNG chuyển lớp sau khi khóa học đã bắt đầu, bất kể cờ chính sách
        // (dữ liệu lịch sử) hay cấu hình phê duyệt.
        Check("TRANSFER_SOURCE_STARTED", !SourceStarted(sourceCohort, now), SourceStartedMessage);

        StudyCohort? target = null;
        if (!targetCohortId.HasValue)
        {
            Check("TRANSFER_TARGET_REQUIRED", false, "Chọn lớp đích để kiểm tra điều kiện chuyển.");
            return new TransferEvaluation(null, policy, checks);
        }
        target = await CohortForTransfer.SingleOrDefaultAsync(x => x.Id == targetCohortId.Value, ct);
        Check("TRANSFER_TARGET_NOT_FOUND", target is not null, "Không tìm thấy lớp đích.", HttpStatusCode.NotFound);
        if (target is null) return new TransferEvaluation(null, policy, checks);

        Check("TRANSFER_TARGET_SAME_COHORT", target.Id != sourceCohort.Id, "Lớp đích phải khác lớp hiện tại.");
        Check("TRANSFER_TARGET_NOT_PUBLISHED", target.Status == "Published", "Lớp đích chưa mở đăng ký.");
        var targetStart = FirstSessionStartUtc(target) ?? target.StartsAt;
        // Chuyển vào lớp đã khai giảng KHÔNG được hỗ trợ khi chưa có quyết định của Product Owner.
        Check("TRANSFER_TARGET_STARTED", targetStart > now,
            "Lớp đích đã bắt đầu; chưa hỗ trợ chuyển vào lớp đang học.");
        Check("TRANSFER_TARGET_CUTOFF", EnrollmentCutoffUtc(target) > now,
            "Lớp đích đã qua hạn khóa đăng ký theo chính sách.");
        Check("TRANSFER_LEVEL_MISMATCH", target.Plan.CertificateLevelId == source.CertificateLevelId,
            "Lớp đích khác cấp độ JLPT của khóa đã mua.");
        Check("TRANSFER_CURRICULUM_MISMATCH", target.Plan.SessionType == "Group"
                && target.Plan.TotalSessions == source.TotalSessions && target.Plan.SessionMinutes == source.SessionMinutes
                && target.Sessions.Count == target.Plan.TotalSessions,
            "Lớp đích khác chương trình (số buổi/thời lượng) của khóa đã mua.");
        var sourcePlanPrice = await uow.StudyCoursePlans.Query().Where(p => p.Id == source.PlanId)
            .Select(p => (decimal?)p.Price).SingleOrDefaultAsync(ct);
        var effectiveSourceTuition = sourceCohort.TotalTuitionSnapshot is decimal srcSnap && srcSnap > 0
            ? srcSnap
            : (source.Amount > 0 ? source.Amount : (sourcePlanPrice ?? 0m));
        var effectiveTargetTuition = target.TotalTuitionSnapshot is decimal dstSnap && dstSnap > 0
            ? dstSnap
            : target.Plan.Price;
        Check("TRANSFER_PAYMENT_POLICY_REQUIRED", target.PlanId == source.PlanId || effectiveTargetTuition == effectiveSourceTuition,
            "Lớp đích có học phí khác; chưa có chính sách thu thêm/hoàn tiền nên không thể chuyển.");
        Check("TRANSFER_TARGET_SCHEDULE_PENDING", target.Sessions.All(s => s.Availability.IsApproved && !s.Availability.IsDeleted
                && s.Availability.RejectedAt == null && s.Availability.ChangeRequestedAt == null),
            "Lịch lớp đích đang chờ xử lý điều chỉnh.", HttpStatusCode.Conflict);
        var targetCapacity = target.CapacitySnapshot ?? target.Capacity;
        var targetPolicy = target.Sessions.Select(s => s.Availability.GroupPolicy).FirstOrDefault(p => p is not null);
        Check("TRANSFER_TARGET_POLICY_CAPACITY", targetPolicy is null || targetCapacity <= targetPolicy.MaxLearners,
            "Sĩ số lớp đích vượt giới hạn chính sách.", HttpStatusCode.Conflict);
        var seatsTaken = target.Enrollments.Count(x => ReservesSeat(x, now));
        Check("TRANSFER_TARGET_FULL", seatsTaken < targetCapacity, "Lớp đích đã hết chỗ.", HttpStatusCode.Conflict);
        Check("TRANSFER_DUPLICATE_ENROLLMENT", !target.Enrollments.Any(x => x.LearnerId == source.LearnerId && ReservesSeat(x, now)),
            "Học viên đã có ghi danh hoặc giữ chỗ ở lớp đích.", HttpStatusCode.Conflict);
        var conflict = await HasLearnerConflictAsync(source.LearnerId, target.Sessions.Select(s => s.Availability).ToList(), source.Id, ct);
        Check("TRANSFER_SCHEDULE_CONFLICT", !conflict, "Lịch lớp đích trùng với lịch học/tư vấn khác của học viên.");
        return new TransferEvaluation(target, policy, checks);
    }

    private static object TransferCohortSummary(StudyCohort c, DateTime now) => new
    {
        c.Id, c.Name, c.Status, c.MentorId, MentorName = $"{c.Mentor?.FirstName} {c.Mentor?.LastName}".Trim(),
        Level = c.Plan?.CertificateLevel?.Code, c.PlanId, c.StartsAt, c.EndsAt,
        Capacity = c.CapacitySnapshot ?? c.Capacity,
        EnrolledCount = c.Enrollments.Count(x => ReservesSeat(x, now)),
        SeatsAvailable = Math.Max(0, (c.CapacitySnapshot ?? c.Capacity) - c.Enrollments.Count(x => ReservesSeat(x, now))),
        EnrollmentClosesAtUtc = c.Sessions.Count == 0 ? c.StartsAt : EnrollmentCutoffUtc(c),
        Sessions = c.Sessions.OrderBy(s => s.Number).Select(s => new { s.Number, s.Availability.StartTime, s.Availability.EndTime })
    };

    private static object TransferView(CohortTransferRequest x) => new
    {
        x.Id, x.Status, x.LearnerId, LearnerName = $"{x.Learner?.FirstName} {x.Learner?.LastName}".Trim(),
        x.SourceCourseId, x.SourceCohortId, SourceCohortName = x.SourceCohort?.Name,
        SourceMentorId = x.SourceCohort?.MentorId,
        x.RequestedTargetCohortId, RequestedTargetCohortName = x.RequestedTargetCohort?.Name,
        x.TargetCohortId, TargetCohortName = x.TargetCohort?.Name,
        x.Reason, HasEvidence = !string.IsNullOrWhiteSpace(x.EvidenceUrl),
        x.CreatedAtUtc, x.ReviewedAtUtc, x.DecisionNote, x.DestinationCourseId, x.PolicyVersionSnapshot, x.Revision,
        History = x.History.OrderBy(h => h.Id).Select(h => new { h.Action, h.ActorRole, h.Detail, h.AtUtc })
    };

    private IQueryable<CohortTransferRequest> TransferDetails => uow.CohortTransferRequests.Query()
        .Include(x => x.Learner).Include(x => x.SourceCohort).Include(x => x.RequestedTargetCohort)
        .Include(x => x.TargetCohort).Include(x => x.History).AsSplitQuery();

    public Task<ApiResponse<object>> RequestTransferAsync(int courseId, int? targetCohortId, string? reason, Guid idempotencyKey,
        IFormFile? evidence, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Rule(idempotencyKey != Guid.Empty, "TRANSFER_IDEMPOTENCY_KEY_REQUIRED", "Thiếu mã chống gửi trùng.");
        var trimmed = reason?.Trim() ?? "";
        Rule(trimmed.Length is >= 10 and <= 1000, "TRANSFER_REASON_REQUIRED", "Nhập lý do chuyển lớp từ 10 đến 1000 ký tự.");
        if (evidence is not null)
            Rule(evidence.Length is > 0 and <= 10 * 1024 * 1024 && AllowedEvidenceTypes.Contains(evidence.ContentType),
                "TRANSFER_EVIDENCE_INVALID", "Minh chứng phải là PDF/PNG/JPG tối đa 10 MB.");
        object result = new { };
        string? evidenceUrl = null;
        var uploaded = false;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(courseId, token);
            Rule(locked is not null, "TRANSFER_SOURCE_NOT_FOUND", "Không tìm thấy ghi danh.", HttpStatusCode.NotFound);
            Rule(locked!.LearnerId == Actor.Id, "TRANSFER_FORBIDDEN", "Bạn chỉ được gửi yêu cầu cho ghi danh của mình.", HttpStatusCode.Forbidden);
            var existing = await TransferDetails.SingleOrDefaultAsync(x => x.LearnerId == Actor.Id && x.IdempotencyKey == idempotencyKey, token);
            if (existing is not null)
            {
                Rule(existing.SourceCourseId == courseId && existing.RequestedTargetCohortId == targetCohortId,
                    "TRANSFER_IDEMPOTENCY_KEY_REUSED", "Mã chống gửi trùng đã dùng cho yêu cầu khác.", HttpStatusCode.Conflict);
                result = TransferView(existing);
                return;
            }
            Rule(locked.CohortId.HasValue && locked.Status == "Active" && locked.PaymentStatus == "Paid",
                "TRANSFER_SOURCE_NOT_ACTIVE", "Chỉ ghi danh lớp nhóm đang học mới được yêu cầu chuyển lớp.", HttpStatusCode.Conflict);
            Rule(!await uow.CohortTransferRequests.AnyAsync(x => x.SourceCourseId == courseId && x.Status == CohortTransferRequest.Pending, token),
                "TRANSFER_REQUEST_ALREADY_PENDING", "Đã có yêu cầu chuyển lớp đang chờ xử lý.", HttpStatusCode.Conflict);
            var policy = await CurrentPolicyAsync(token);
            Rule(policy.LearnerTransferEnabled, "TRANSFER_DISABLED_BY_POLICY", "Chính sách hiện hành chưa cho phép chuyển lớp.");
            var sourceCohortForStart = await uow.StudyCohorts.Query().AsNoTracking()
                .Include(x => x.Sessions).ThenInclude(x => x.Availability)
                .SingleAsync(x => x.Id == locked.CohortId!.Value, token);
            Rule(!SourceStarted(sourceCohortForStart, DateTime.UtcNow), "TRANSFER_SOURCE_STARTED", SourceStartedMessage);
            if (targetCohortId.HasValue)
            {
                var sourceCohortId = locked.CohortId!.Value;
                var target = await uow.StudyCohorts.Query().Include(x => x.Plan).Include(x => x.Sessions).ThenInclude(x => x.Availability)
                    .SingleOrDefaultAsync(x => x.Id == targetCohortId.Value, token);
                Rule(target is not null, "TRANSFER_TARGET_NOT_FOUND", "Không tìm thấy lớp đích.", HttpStatusCode.NotFound);
                Rule(target!.Id != sourceCohortId, "TRANSFER_TARGET_SAME_COHORT", "Lớp đích phải khác lớp hiện tại.");
                Rule(target.Status == "Published", "TRANSFER_TARGET_NOT_PUBLISHED", "Lớp đích chưa mở đăng ký.");
                Rule(target.Plan.CertificateLevelId == locked.CertificateLevelId, "TRANSFER_LEVEL_MISMATCH", "Lớp đích khác cấp độ JLPT của khóa đã mua.");
                Rule((FirstSessionStartUtc(target) ?? target.StartsAt) > DateTime.UtcNow, "TRANSFER_TARGET_STARTED",
                    "Lớp đích đã bắt đầu; chưa hỗ trợ chuyển vào lớp đang học.");
            }
            if (evidence is not null && !uploaded)
            {
                await using var stream = evidence.OpenReadStream();
                evidenceUrl = (await storage.UploadPrivateVerificationAsync(stream, evidence.ContentType, Actor.Id, token)).Url;
                uploaded = true;
            }
            var request = new CohortTransferRequest
            {
                LearnerId = Actor.Id, SourceCourseId = courseId, SourceCohortId = locked.CohortId!.Value,
                RequestedTargetCohortId = targetCohortId, Reason = trimmed, EvidenceUrl = evidenceUrl,
                IdempotencyKey = idempotencyKey, PolicyId = policy.Id == 0 ? null : policy.Id,
                PolicyVersionSnapshot = policy.Version == 0 ? null : policy.Version, CreatedAtUtc = DateTime.UtcNow
            };
            AddTransferEvent(request, "Requested", targetCohortId.HasValue
                ? $"Học viên đề xuất chuyển sang lớp #{targetCohortId}." : "Học viên đề nghị Quản trị viên chọn lớp phù hợp.");
            await uow.CohortTransferRequests.AddAsync(request, token);
            var course = await uow.StudyCourses.Query().Include(x => x.Audit).SingleAsync(x => x.Id == courseId, token);
            Audit(course, "CohortTransferRequested", "Học viên đã gửi yêu cầu chuyển lớp; đang chờ Quản trị viên xem xét.");
            await uow.SaveChangesAsync(token);
            var admins = await uow.UserAccounts.Query().Where(u => u.Role == Role.Admin && !u.IsDeleted).Select(u => u.Id).ToListAsync(token);
            QueueNotifications($"cohort-transfer:{request.Id}:requested", "CohortTransferRequest", request.Id.ToString(),
                admins.Select(id => (id, "Yêu cầu chuyển lớp mới", "Có học viên gửi yêu cầu chuyển lớp cần xem xét.",
                    "CohortTransfer", (string?)$"/admin/study-courses?transferRequestId={request.Id}")));
            await uow.SaveChangesAsync(token);
            result = TransferView(await TransferDetails.SingleAsync(x => x.Id == request.Id, token));
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> TransferRequestsAsync(string? status, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner)) throw new UnauthorizedAccessException();
        var actor = Actor;
        var query = TransferDetails.Where(x => actor.Role == Role.Admin || x.LearnerId == actor.Id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
        return rows.Select(TransferView).ToList();
    });

    public Task<ApiResponse<object>> TransferRequestDetailAsync(int requestId, CancellationToken ct) => Run(async () =>
    {
        var request = await TransferDetails.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        Rule(request is not null, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
        if (Actor.Role == Role.Learner)
        {
            // Không tiết lộ yêu cầu của Học viên khác.
            Rule(request!.LearnerId == Actor.Id, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
            return TransferView(request);
        }
        if (Actor.Role != Role.Admin) throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        var source = await uow.StudyCourses.Query().AsNoTracking().Include(x => x.Sessions).Include(x => x.Homework)
            .SingleAsync(x => x.Id == request!.SourceCourseId, ct);
        var sourceCohort = await CohortForTransfer.AsNoTracking().SingleAsync(x => x.Id == request!.SourceCohortId, ct);
        var assignments = await uow.HomeworkAssignments.Query().AsNoTracking().Include(h => h.Submission)
            .Where(h => !h.IsDeleted && h.StudyCourseId == source.Id).ToListAsync(ct);
        var candidates = await CohortForTransfer.AsNoTracking()
            .Where(x => x.Id != sourceCohort.Id && x.Status == "Published" && x.Plan.CertificateLevelId == source.CertificateLevelId)
            .ToListAsync(ct);
        var targetId = request!.TargetCohortId ?? request.RequestedTargetCohortId;
        var evaluation = request.Status == CohortTransferRequest.Pending
            ? await EvaluateTransferAsync(source, sourceCohort, targetId, ct) : null;
        return new
        {
            Request = TransferView(request),
            SourceEnrollment = new
            {
                source.Id, source.Status, source.PaymentStatus, source.StartsAt, source.EndsAt, source.MentorId,
                Progress = new
                {
                    source.TotalSessions,
                    Attended = source.Sessions.Count(s => s.Status == "Present"),
                    Absent = source.Sessions.Count(s => s.Status is "Absent" or "ExcusedAbsent"),
                    Upcoming = source.Sessions.Count(s => s.Status == "Scheduled" && s.ScheduledAt > now),
                    HomeworkAssigned = assignments.Count + source.Homework.Count,
                    HomeworkSubmitted = assignments.Count(h => h.Submission is { IsDeleted: false }) + source.Homework.Count(h => h.SubmittedAt.HasValue)
                }
            },
            SourceCohort = TransferCohortSummary(sourceCohort, now),
            CandidateCohorts = candidates.Where(c => (FirstSessionStartUtc(c) ?? c.StartsAt) > now)
                .OrderBy(c => c.StartsAt).Select(c => TransferCohortSummary(c, now)).ToList(),
            Eligibility = evaluation is null ? null : new
            {
                TargetCohortId = targetId, evaluation.Eligible, PolicyVersion = evaluation.Policy.Version,
                Checks = evaluation.Checks.Select(c => new { c.Code, c.Passed, c.Message })
            }
        };
    });

    public Task<ApiResponse<object>> TransferEligibilityAsync(int requestId, int targetCohortId, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var request = await uow.CohortTransferRequests.Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        Rule(request is not null, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
        var source = await uow.StudyCourses.Query().AsNoTracking().SingleAsync(x => x.Id == request!.SourceCourseId, ct);
        var sourceCohort = await CohortForTransfer.AsNoTracking().SingleAsync(x => x.Id == request!.SourceCohortId, ct);
        var evaluation = await EvaluateTransferAsync(source, sourceCohort, targetCohortId, ct);
        return new
        {
            TargetCohortId = targetCohortId, evaluation.Eligible, PolicyVersion = evaluation.Policy.Version,
            Target = evaluation.Target is null ? null : TransferCohortSummary(evaluation.Target, DateTime.UtcNow),
            Checks = evaluation.Checks.Select(c => new { c.Code, c.Passed, c.Message })
        };
    });

    public Task<ApiResponse<object>> ApproveTransferAsync(int requestId, ApproveCohortTransfer r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(r.Note is null || r.Note.Trim().Length <= 1000, "TRANSFER_NOTE_TOO_LONG", "Ghi chú tối đa 1000 ký tự.");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortTransferRequests.Query().AsNoTracking()
                .Select(x => new { x.Id, x.SourceCourseId, x.LearnerId }).SingleOrDefaultAsync(x => x.Id == requestId, token);
            Rule(header is not null, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
            // Khóa hàng ghi danh nguồn: tuần tự hóa mọi quyết định trên cùng yêu cầu/ghi danh.
            await uow.StudyCourses.LockAsync(header!.SourceCourseId, token);
            var request = await uow.CohortTransferRequests.Query().Include(x => x.History).SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortTransferRequest.Approved)
            {
                Rule(!r.TargetCohortId.HasValue || r.TargetCohortId == request.TargetCohortId, "TRANSFER_REQUEST_NOT_PENDING",
                    "Yêu cầu đã được duyệt sang lớp khác.", HttpStatusCode.Conflict);
                result = new { request.Id, request.Status, request.TargetCohortId, request.DestinationCourseId, Idempotent = true };
                return;
            }
            Rule(request.Status == CohortTransferRequest.Pending, "TRANSFER_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            Rule(!r.ExpectedRevision.HasValue || r.ExpectedRevision == request.Revision, "TRANSFER_REQUEST_STALE",
                "Yêu cầu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict);
            var targetId = r.TargetCohortId ?? request.RequestedTargetCohortId;
            Rule(targetId.HasValue, "TRANSFER_TARGET_REQUIRED", "Chọn lớp đích trước khi duyệt.");
            await uow.StudyCourses.LockScheduleAsync(request.LearnerId, request.LearnerId, token);
            foreach (var cohortId in new[] { request.SourceCohortId, targetId!.Value }.Distinct().OrderBy(x => x))
                await uow.StudyCourses.LockCohortAsync(cohortId, token);

            var source = await uow.StudyCourses.Query().Include(x => x.Sessions).Include(x => x.Audit).SingleAsync(x => x.Id == request.SourceCourseId, token);
            var sourceCohort = await CohortForTransfer.SingleAsync(x => x.Id == request.SourceCohortId, token);
            // Kiểm tra lại toàn bộ điều kiện tại thời điểm duyệt (sau khi giữ khóa).
            var evaluation = await EvaluateTransferAsync(source, sourceCohort, targetId, token);
            var failure = evaluation.FirstFailure;
            if (failure is not null) throw new CourseRuleException(failure.Message, (HttpStatusCode)failure.Status, failure.Code);
            var target = evaluation.Target!;
            var now = DateTime.UtcNow;

            source.Status = "Transferred";
            source.TransferredAtUtc = now;
            foreach (var s in source.Sessions.Where(s => s.Status == "Scheduled" && s.ScheduledAt > now))
            {
                // Buổi tương lai của lớp nguồn: thu hồi quyền vào phòng, KHÔNG xóa; buổi đã diễn ra giữ nguyên điểm danh.
                s.Status = "TransferredOut"; s.Note = $"Chuyển sang lớp #{target.Id} theo yêu cầu #{request.Id}.";
                s.ConfirmedById = Actor.Id; s.ConfirmedAt = now;
            }
            var targetPolicy = target.Sessions.Select(s => s.Availability.GroupPolicy).FirstOrDefault(p => p is not null);
            var destination = new StudyCourse
            {
                CohortId = target.Id, MentorId = target.MentorId, StartsAt = target.StartsAt, EndsAt = target.EndsAt,
                SessionType = "Group", TargetExamDate = target.Plan.TargetExamDate, LearnerId = source.LearnerId,
                PurchaseKey = Guid.NewGuid(), PlanId = target.PlanId, Name = target.Plan.Name,
                CertificateLevelId = target.Plan.CertificateLevelId, PlanRevision = target.Plan.Revision,
                DurationMonths = target.Plan.DurationMonths, SessionsPerWeek = target.Plan.SessionsPerWeek,
                TotalSessions = target.Plan.TotalSessions!.Value, SessionMinutes = target.Plan.SessionMinutes!.Value,
                // Giữ nguyên điều kiện quyền lợi đã đóng băng khi mua ở ghi danh nguồn.
                RetakeClaimDays = source.RetakeClaimDays, MinimumHomeworkPercent = source.MinimumHomeworkPercent,
                MaximumAbsencePercent = source.MaximumAbsencePercent,
                GroupPolicyId = targetPolicy?.Id ?? target.GroupPolicyId, PolicyVersionSnapshot = targetPolicy?.Version ?? target.PolicyVersionSnapshot,
                CapacitySnapshot = target.CapacitySnapshot ?? target.Capacity,
                // Không thu thêm, không hoàn tiền, không tiêu thụ thêm quyền học: quyền đã thanh toán ở nguồn được chuyển sang.
                Amount = 0, PaymentStatus = "Paid", Status = "Active", PaymentReference = $"TRANSFER:{source.Id}",
                TransferredFromCourseId = source.Id, TransferredAtUtc = now
            };
            AddCohortSessions(destination, target);
            Audit(destination, "CohortTransferIn", $"Chuyển từ ghi danh #{source.Id} (lớp #{sourceCohort.Id}) theo yêu cầu #{request.Id}.");
            Audit(source, "CohortTransferOut", $"Đã chuyển sang lớp {target.Name}; lịch sử điểm danh và bài tập về nhà được giữ nguyên.");
            await uow.StudyCourses.AddAsync(destination, token);
            await uow.SaveChangesAsync(token);

            request.Status = CohortTransferRequest.Approved; request.TargetCohortId = target.Id;
            request.DestinationCourseId = destination.Id; request.ReviewedById = Actor.Id; request.ReviewedAtUtc = now;
            request.DecisionNote = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
            request.PolicyId = evaluation.Policy.Id == 0 ? request.PolicyId : evaluation.Policy.Id;
            request.PolicyVersionSnapshot = evaluation.Policy.Version == 0 ? request.PolicyVersionSnapshot : evaluation.Policy.Version;
            request.EligibilitySnapshot = JsonSerializer.Serialize(evaluation.Checks.Select(c => new { c.Code, c.Passed }));
            request.Revision++;
            AddTransferEvent(request, "Approved", $"Quản trị viên đã phê duyệt chuyển sang lớp {target.Name}.");
            QueueNotifications($"cohort-transfer:{request.Id}:approved", "CohortTransferRequest", request.Id.ToString(),
            [
                (request.LearnerId, "Yêu cầu chuyển lớp đã được duyệt", $"Bạn đã được chuyển sang lớp {target.Name}.", "CohortTransfer", $"/my-courses/{destination.Id}"),
                (sourceCohort.MentorId, "Thay đổi danh sách lớp", $"Một học viên đã chuyển khỏi lớp {sourceCohort.Name}.", "CohortRoster", $"/mentor/cohorts/{sourceCohort.Id}"),
                (target.MentorId, "Thay đổi danh sách lớp", $"Một học viên mới chuyển vào lớp {target.Name}.", "CohortRoster", $"/mentor/cohorts/{target.Id}")
            ]);
            await uow.SaveChangesAsync(token);
            result = new { request.Id, request.Status, request.TargetCohortId, request.DestinationCourseId, Idempotent = false };
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> RejectTransferAsync(int requestId, RejectCohortTransfer r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var reason = r.Reason?.Trim() ?? "";
        Rule(reason.Length is >= 1 and <= 1000, "TRANSFER_REJECTION_REASON_REQUIRED", "Nhập lý do từ chối (tối đa 1000 ký tự).");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortTransferRequests.Query().AsNoTracking().Select(x => new { x.Id, x.SourceCourseId })
                .SingleOrDefaultAsync(x => x.Id == requestId, token);
            Rule(header is not null, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
            await uow.StudyCourses.LockAsync(header!.SourceCourseId, token);
            var request = await uow.CohortTransferRequests.Query().Include(x => x.History).SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortTransferRequest.Rejected)
            {
                result = new { request.Id, request.Status, request.DecisionNote, Idempotent = true };
                return;
            }
            Rule(request.Status == CohortTransferRequest.Pending, "TRANSFER_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            Rule(!r.ExpectedRevision.HasValue || r.ExpectedRevision == request.Revision, "TRANSFER_REQUEST_STALE",
                "Yêu cầu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict);
            request.Status = CohortTransferRequest.Rejected; request.DecisionNote = reason;
            request.ReviewedById = Actor.Id; request.ReviewedAtUtc = DateTime.UtcNow; request.Revision++;
            AddTransferEvent(request, "Rejected", reason);
            var source = await uow.StudyCourses.Query().Include(x => x.Audit).SingleAsync(x => x.Id == request.SourceCourseId, token);
            Audit(source, "CohortTransferRejected", $"Yêu cầu #{request.Id} bị từ chối: {reason}");
            QueueNotifications($"cohort-transfer:{request.Id}:rejected", "CohortTransferRequest", request.Id.ToString(),
                [(request.LearnerId, "Yêu cầu chuyển lớp bị từ chối", reason, "CohortTransfer", $"/my-courses/{request.SourceCourseId}")]);
            await uow.SaveChangesAsync(token);
            result = new { request.Id, request.Status, request.DecisionNote, Idempotent = false };
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> WithdrawTransferAsync(int requestId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortTransferRequests.Query().AsNoTracking().Select(x => new { x.Id, x.SourceCourseId, x.LearnerId })
                .SingleOrDefaultAsync(x => x.Id == requestId, token);
            Rule(header is not null && header.LearnerId == Actor.Id, "TRANSFER_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu chuyển lớp.", HttpStatusCode.NotFound);
            await uow.StudyCourses.LockAsync(header!.SourceCourseId, token);
            var request = await uow.CohortTransferRequests.Query().Include(x => x.History).SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortTransferRequest.Withdrawn) { result = new { request.Id, request.Status, Idempotent = true }; return; }
            Rule(request.Status == CohortTransferRequest.Pending, "TRANSFER_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            request.Status = CohortTransferRequest.Withdrawn; request.Revision++;
            AddTransferEvent(request, "Withdrawn", "Học viên rút yêu cầu.");
            await uow.SaveChangesAsync(token);
            result = new { request.Id, request.Status, Idempotent = false };
        }, ct);
        return result;
    });

    public async Task<PrivateVerificationReadResult?> TransferEvidenceAsync(int requestId, CancellationToken ct)
    {
        var request = await uow.CohortTransferRequests.Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null || string.IsNullOrWhiteSpace(request.EvidenceUrl)
            || (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && request.LearnerId == Actor.Id))) return null;
        return await storage.OpenPrivateVerificationAsync(request.EvidenceUrl, ct);
    }
}
