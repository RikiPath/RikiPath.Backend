using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

// Vòng đời lớp chưa có học viên tại thời điểm quyết định khai giảng (quyết định Product Owner):
// - Lớp có thể mở với 1..4 học viên; lớp đã công bố có thể trống trước hạn đăng ký.
// - Lớp KHÔNG có học viên xác nhận: Mentor chính (hoặc Admin) yêu cầu dời TOÀN BỘ khóa thêm N tuần (mặc định 1),
//   giữ nguyên thứ/giờ, số buổi và số buổi/tuần; hoặc yêu cầu hủy lớp trống.
// - Admin duyệt từng yêu cầu (mỗi yêu cầu là một quyết định riêng, có lịch sử); không giới hạn số lần dời,
//   nhưng mỗi lớp chỉ có một yêu cầu Pending; không có vòng lặp tự dời nền.
// - Không dời/hủy khi còn giữ chỗ thanh toán, đơn chờ đối soát (<24h sau hết hạn) hoặc đơn PaymentReview; giữ nguyên bằng chứng thanh toán.
// Dời lịch dùng đúng dịch vụ kiểm tra trùng lịch dạy chuẩn (IMentorTeachingConflictService) dưới LockScheduleAsync(mentor).
public sealed partial class StudyCourseService
{
    public const int MaxCohortShiftWeeks = 8;
    private const int PaymentReconciliationWindowHours = 24;

    private sealed record ShiftedCohortSession(StudyCohortSession Session, DateTime ProposedStart, DateTime ProposedEnd);
    private sealed record CohortPaymentBlocker(int CourseId, string Kind, string PaymentStatus, string Status, DateTime? PaymentExpiresAt);

    private sealed record CohortScheduleEvaluation(StudyCohort? Cohort, string Kind, int? Weeks, List<TransferCheck> Checks,
        List<CohortPaymentBlocker> Blockers, List<ShiftedCohortSession> Shifted, TeachingConflictReport? Report, DateTime? NewCutoffUtc)
    {
        public bool Eligible => Checks.All(c => c.Passed);
        public TransferCheck? FirstFailure => Checks.FirstOrDefault(c => !c.Passed);
    }

    private IQueryable<CohortScheduleRequest> ScheduleRequestDetails => uow.CohortScheduleRequests.Query()
        .Include(x => x.Cohort).Include(x => x.History).AsSplitQuery();

    private void AddScheduleRequestEvent(CohortScheduleRequest request, string action, string detail)
        => request.History.Add(new CohortScheduleRequestEvent
        {
            Action = action, ActorId = Actor.Id, ActorRole = Actor.Role.ToString(),
            Detail = detail.Length > 2000 ? detail[..2000] : detail, AtUtc = DateTime.UtcNow
        });

    // Đơn thanh toán chặn dời/hủy lớp; bằng chứng thanh toán luôn được giữ nguyên.
    private static CohortPaymentBlocker? PaymentBlockerOf(StudyCourse e, DateTime now)
    {
        if (e.Status == "PaymentReview")
            return new(e.Id, "PaymentReview", e.PaymentStatus, e.Status, e.PaymentExpiresAt);
        if (e.PaymentStatus != "Pending") return null;
        if (e.PaymentExpiresAt > now)
            return new(e.Id, "CheckoutHold", e.PaymentStatus, e.Status, e.PaymentExpiresAt);
        var reference = e.PaymentExpiresAt ?? e.CreatedAt;
        if (!string.IsNullOrWhiteSpace(e.CheckoutUrl) && reference > now.AddHours(-PaymentReconciliationWindowHours))
            return new(e.Id, "AwaitingReconciliation", e.PaymentStatus, e.Status, e.PaymentExpiresAt);
        return null;
    }

    // Toàn bộ điều kiện dời/hủy lớp; gọi khi xem trước, khi tạo yêu cầu và gọi lại trong giao dịch khi duyệt.
    private async Task<CohortScheduleEvaluation> EvaluateCohortScheduleAsync(int cohortId, string kind, int? weeks, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var checks = new List<TransferCheck>();
        void Check(string code, bool passed, string message, HttpStatusCode status = HttpStatusCode.Conflict)
            => checks.Add(new TransferCheck(code, passed, message, (int)status));
        var isShift = kind == CohortScheduleRequest.KindShiftWeeks;
        if (isShift)
            Check("COHORT_SHIFT_WEEKS_INVALID", weeks is >= 1 and <= MaxCohortShiftWeeks,
                $"Số tuần dời phải từ 1 đến {MaxCohortShiftWeeks}.", HttpStatusCode.UnprocessableEntity);

        var cohort = await CohortForTransfer.SingleOrDefaultAsync(x => x.Id == cohortId, ct);
        Check("COHORT_NOT_FOUND", cohort is not null, "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        if (cohort is null) return new(null, kind, weeks, checks, [], [], null, null);

        Check("COHORT_NOT_ACTIVE", cohort.Status is "Published" or "Draft", "Lớp đã đóng hoặc đã hủy; không thể dời/hủy lịch.");
        Check("COHORT_ALREADY_STARTED", (FirstSessionStartUtc(cohort) ?? cohort.StartsAt) > now,
            "Lớp đã bắt đầu; chỉ dời/hủy được lớp chưa bắt đầu.");
        var blockers = cohort.Enrollments.Select(e => PaymentBlockerOf(e, now)).OfType<CohortPaymentBlocker>().ToList();
        Check("COHORT_PAYMENT_REVIEW_OPEN", !blockers.Any(b => b.Kind == "PaymentReview"),
            "Lớp có giao dịch thanh toán đang được Quản trị viên xem xét; cần xử lý xong trước khi dời hoặc hủy lịch.");
        Check("COHORT_PAYMENT_PENDING", !blockers.Any(b => b.Kind is "CheckoutHold" or "AwaitingReconciliation"),
            "Lớp có học viên đang giữ chỗ thanh toán hoặc đơn thanh toán chờ đối soát; cần đối soát trước khi dời/hủy lịch.");
        var confirmed = cohort.Enrollments.Count(e => e.PaymentStatus is "Paid" or "PartiallyRefunded"
            && e.Status is not ("PaymentReview" or "Cancelled" or "Transferred" or "PendingPayment" or "PaymentExpired" or "PaymentFailed")
            && ReservesSeat(e, now));
        Check("COHORT_HAS_CONFIRMED_LEARNERS", confirmed == 0,
            $"Lớp đã có {confirmed} học viên xác nhận; chỉ dời/hủy được lớp chưa có học viên.");

        var ordered = cohort.Sessions.OrderBy(s => s.Number).ToList();
        if (isShift)
            Check("COHORT_HAS_NO_SESSIONS", ordered.Count > 0, "Lớp chưa có buổi học nào để dời.", HttpStatusCode.UnprocessableEntity);
        Check("COHORT_SESSION_LOCKED", ordered.All(s => s.Availability.IsApproved && !s.Availability.IsDeleted
                && s.Availability.RejectedAt == null && s.Availability.ChangeRequestedAt == null && s.Availability.MentorId == cohort.MentorId),
            "Có buổi học đang chờ điều chỉnh lịch hoặc không còn thuộc Giáo viên phụ trách của lớp.");
        Check("COHORT_SCHEDULE_CHANGE_PENDING", !await uow.CourseScheduleRequests.Query()
                .AnyAsync(x => x.Status == "Pending" && x.Course.CohortId == cohortId, ct),
            "Có yêu cầu đổi/hủy buổi đang chờ xử lý trong lớp.");

        var shifted = new List<ShiftedCohortSession>();
        TeachingConflictReport? report = null;
        DateTime? newCutoff = null;
        if (isShift && weeks is >= 1 and <= MaxCohortShiftWeeks && ordered.Count > 0)
        {
            var days = 7 * weeks.Value;
            // Giờ Việt Nam không có giờ mùa hè: cộng đúng 7*N ngày giữ nguyên thứ/giờ, số buổi và số buổi mỗi tuần.
            shifted = ordered.Select(s => new ShiftedCohortSession(s, s.Availability.StartTime.AddDays(days), s.Availability.EndTime.AddDays(days))).ToList();
            var policy = await CurrentPolicyAsync(ct);
            report = await TeachingConflicts.CheckAsync(cohort.MentorId,
                shifted.Select(x => new ProposedTeachingInterval(x.ProposedStart, x.ProposedEnd, x.Session.Number)).ToList(),
                new TeachingConflictOptions
                {
                    // Các khung giờ cũ của chính lớp sẽ bị thu hồi, không tính là cam kết.
                    ExcludeCohortId = cohort.Id,
                    RestMinutes = policy.Version == 0 ? null : policy.RestMinutes,
                    DailyCap = policy.Version == 0 ? null : policy.DailySessionCap,
                    WeeklyCap = policy.Version == 0 ? null : policy.WeeklySessionCap,
                    // Mẫu lịch của lớp đã hợp lệ và chỉ tịnh tiến nguyên khối.
                    IncludeOwnIntervals = false
                }, ct);
            var first = report.First;
            checks.Add(new TransferCheck(first?.Code ?? MentorTeachingConflictService.ScheduleConflict, report.IsValid,
                report.IsValid ? "Giáo viên không bị trùng lịch dạy trong các tuần mới."
                    : $"Buổi {first!.SessionNumber} sau khi dời bị trùng lịch: {first.Message}", (int)HttpStatusCode.Conflict));
            var firstShift = shifted.MinBy(x => x.ProposedStart)!;
            newCutoff = firstShift.ProposedStart.AddMinutes(
                -(firstShift.Session.Availability.GroupPolicy?.EnrollmentCutoffMinutesBeforeFirstSession ?? 0));
        }
        return new(cohort, kind, weeks, checks, blockers, shifted, report, newCutoff);
    }

    private object CohortSchedulePreview(CohortScheduleEvaluation e, bool includeCourseIds)
    {
        var c = e.Cohort!;
        return new
        {
            CohortId = c.Id, CohortName = c.Name, CohortStatus = c.Status, CohortRevision = c.Revision, c.MentorId,
            e.Kind, ShiftWeeks = e.Weeks, e.Eligible,
            Checks = e.Checks.Select(x => new { x.Code, x.Passed, x.Message }).ToList(),
            Blockers = e.Blockers.Select(b => new
            {
                CourseId = includeCourseIds ? b.CourseId : (int?)null, b.Kind, b.PaymentStatus, b.Status, b.PaymentExpiresAt
            }).ToList(),
            SessionCount = c.Sessions.Count, c.Plan?.SessionsPerWeek,
            CurrentStartsAt = c.StartsAt, CurrentEndsAt = c.EndsAt,
            ProposedStartsAt = e.Shifted.Count == 0 ? (DateTime?)null : c.StartsAt.AddDays(7 * e.Weeks!.Value),
            ProposedEndsAt = e.Shifted.Count == 0 ? (DateTime?)null : c.EndsAt.AddDays(7 * e.Weeks!.Value),
            CurrentEnrollmentCutoffUtc = EnrollmentCutoffUtc(c),
            NewEnrollmentCutoffUtc = e.NewCutoffUtc,
            ProposedSessions = e.Shifted.Select(x => new
            {
                x.Session.Number, x.Session.AvailabilityId,
                CurrentStart = x.Session.Availability.StartTime, CurrentEnd = x.Session.Availability.EndTime,
                ProposedStart = x.ProposedStart, ProposedEnd = x.ProposedEnd
            }).ToList(),
            Conflicts = (e.Report?.Conflicts ?? []).Select(x => new
            {
                x.SessionNumber, x.Code, Kind = x.CommitmentKind, x.CohortId, x.CohortName,
                x.ProposedStartUtc, x.ProposedEndUtc, x.CommitmentStartUtc, x.CommitmentEndUtc, x.Message
            }).ToList()
        };
    }

    private static JsonElement? JsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static object ScheduleRequestView(CohortScheduleRequest x, object? livePreview, bool? idempotent = null) => new
    {
        x.Id, x.CohortId, CohortName = x.Cohort?.Name, x.Kind, x.ShiftWeeks, x.Reason, x.Status,
        x.RequestedById, x.RequestedByRole, x.CohortRevisionSnapshot, ImpactSnapshot = JsonOrNull(x.ImpactSnapshot),
        x.ReviewedById, x.ReviewedAtUtc, x.DecisionNote, x.CreatedAtUtc, x.Revision,
        History = x.History.OrderBy(h => h.Id).Select(h => new { h.Action, h.ActorId, h.ActorRole, h.Detail, h.AtUtc }).ToList(),
        Preview = livePreview, Idempotent = idempotent
    };

    // Admin, hoặc Mentor chính HIỆN TẠI của lớp; trả về Mentor chính.
    private async Task<int> EnsureCohortScheduleActorAsync(int cohortId, CancellationToken ct)
    {
        var mentorId = await uow.StudyCohorts.Query().AsNoTracking().Where(x => x.Id == cohortId)
            .Select(x => (int?)x.MentorId).SingleOrDefaultAsync(ct);
        Rule(mentorId.HasValue, "COHORT_NOT_FOUND", "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        if (Actor.Role == Role.Admin || (Actor.Role == Role.Mentor && mentorId == Actor.Id)) return mentorId!.Value;
        throw new UnauthorizedAccessException();
    }

    private Task<List<int>> ActiveAdminIdsAsync(CancellationToken ct)
        => uow.UserAccounts.Query().Where(u => u.Role == Role.Admin && !u.IsDeleted).Select(u => u.Id).ToListAsync(ct);

    public Task<ApiResponse<object>> CohortShiftPreviewAsync(int cohortId, int weeks, CancellationToken ct) => Run(async () =>
    {
        await EnsureCohortScheduleActorAsync(cohortId, ct);
        Rule(weeks is >= 1 and <= MaxCohortShiftWeeks, "COHORT_SHIFT_WEEKS_INVALID", $"Số tuần dời phải từ 1 đến {MaxCohortShiftWeeks}.");
        var e = await EvaluateCohortScheduleAsync(cohortId, CohortScheduleRequest.KindShiftWeeks, weeks, ct);
        Rule(e.Cohort is not null, "COHORT_NOT_FOUND", "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        return CohortSchedulePreview(e, Actor.Role == Role.Admin);
    });

    public Task<ApiResponse<object>> CreateCohortScheduleRequestAsync(int cohortId, CreateCohortScheduleRequest r, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Mentor)) throw new UnauthorizedAccessException();
        await EnsureCohortScheduleActorAsync(cohortId, ct);
        Rule(r is not null, "COHORT_SCHEDULE_REQUEST_KIND_INVALID", "Thiếu nội dung yêu cầu.");
        var kind = r!.Kind?.Trim() ?? "";
        Rule(kind is CohortScheduleRequest.KindShiftWeeks or CohortScheduleRequest.KindCancelEmptyCohort,
            "COHORT_SCHEDULE_REQUEST_KIND_INVALID", "Loại yêu cầu không hợp lệ (chỉ hỗ trợ dời lịch lớp hoặc hủy lớp chưa có học viên).");
        int? weeks = kind == CohortScheduleRequest.KindShiftWeeks ? r.ShiftWeeks ?? 1 : null;
        Rule(weeks is null || weeks is >= 1 and <= MaxCohortShiftWeeks, "COHORT_SHIFT_WEEKS_INVALID",
            $"Số tuần dời phải từ 1 đến {MaxCohortShiftWeeks}.");
        var reason = r.Reason?.Trim() ?? "";
        Rule(reason.Length is >= 10 and <= 1000, "COHORT_SCHEDULE_REQUEST_REASON_REQUIRED", "Nhập lý do từ 10 đến 1000 ký tự.");
        Rule(r.IdempotencyKey != Guid.Empty, "COHORT_SCHEDULE_IDEMPOTENCY_KEY_REQUIRED", "Thiếu mã chống gửi trùng.");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var existing = await ScheduleRequestDetails.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestedById == Actor.Id && x.IdempotencyKey == r.IdempotencyKey, token);
            if (existing is not null)
            {
                Rule(existing.CohortId == cohortId && existing.Kind == kind && existing.ShiftWeeks == weeks,
                    "COHORT_SCHEDULE_IDEMPOTENCY_KEY_REUSED", "Mã chống gửi trùng đã dùng cho yêu cầu khác.", HttpStatusCode.Conflict);
                result = ScheduleRequestView(existing, null, idempotent: true);
                return;
            }
            await uow.StudyCourses.LockCohortAsync(cohortId, token);
            // Kiểm tra lại sau khi giữ khóa: Mentor chính có thể vừa được thay.
            await EnsureCohortScheduleActorAsync(cohortId, token);
            Rule(!await uow.CohortScheduleRequests.AnyAsync(x => x.CohortId == cohortId && x.Status == CohortScheduleRequest.Pending, token),
                "COHORT_SCHEDULE_REQUEST_PENDING", "Lớp đang có một yêu cầu dời/hủy lịch chờ Quản trị viên xử lý.", HttpStatusCode.Conflict);
            var e = await EvaluateCohortScheduleAsync(cohortId, kind, weeks, token);
            // Điều kiện cấu trúc chặn việc tạo yêu cầu; giao dịch chờ đối soát và trùng lịch chỉ được liệt kê trong bản xem trước.
            foreach (var code in new[] { "COHORT_NOT_FOUND", "COHORT_NOT_ACTIVE", "COHORT_ALREADY_STARTED", "COHORT_HAS_CONFIRMED_LEARNERS", "COHORT_HAS_NO_SESSIONS" })
            {
                var failed = e.Checks.FirstOrDefault(c => c.Code == code && !c.Passed);
                if (failed is not null) throw new CourseRuleException(failed.Message, (HttpStatusCode)failed.Status, failed.Code);
            }
            var cohort = e.Cohort!;
            var now = DateTime.UtcNow;
            var request = new CohortScheduleRequest
            {
                CohortId = cohort.Id, Kind = kind, ShiftWeeks = weeks, Reason = reason, Status = CohortScheduleRequest.Pending,
                RequestedById = Actor.Id, RequestedByRole = Actor.Role.ToString(), IdempotencyKey = r.IdempotencyKey,
                CohortRevisionSnapshot = cohort.Revision, ImpactSnapshot = JsonSerializer.Serialize(CohortSchedulePreview(e, includeCourseIds: false)),
                CreatedAtUtc = now
            };
            AddScheduleRequestEvent(request, "Requested", kind == CohortScheduleRequest.KindShiftWeeks
                ? $"Đề nghị dời toàn bộ {e.Shifted.Count} buổi của lớp thêm {weeks} tuần. {reason}"
                : $"Đề nghị hủy lớp chưa có học viên. {reason}");
            if (e.Blockers.Count > 0)
                AddScheduleRequestEvent(request, "PaymentBlockerDetected",
                    $"Có {e.Blockers.Count} giao dịch cần đối soát trước khi duyệt: {string.Join(", ", e.Blockers.Select(b => $"#{b.CourseId} {b.Kind}"))}.");
            await uow.CohortScheduleRequests.AddAsync(request, token);
            await uow.SaveChangesAsync(token);

            var admins = await ActiveAdminIdsAsync(token);
            var link = $"/admin/study-courses?cohortScheduleRequestId={request.Id}";
            QueueNotifications($"cohort-schedule:{request.Id}:requested", "CohortScheduleRequest", request.Id.ToString(),
                admins.Select(id => (id,
                    kind == CohortScheduleRequest.KindShiftWeeks ? "Yêu cầu dời lịch lớp mới" : "Yêu cầu hủy lớp trống",
                    kind == CohortScheduleRequest.KindShiftWeeks
                        ? $"Lớp {cohort.Name} chưa có học viên; đề nghị dời toàn bộ lịch thêm {weeks} tuần."
                        : $"Lớp {cohort.Name} chưa có học viên; đề nghị hủy lớp.",
                    "CohortScheduleRequest", (string?)link)));
            if (e.Blockers.Count > 0)
                QueueNotifications($"cohort-schedule:{request.Id}:payment-blocker", "StudyCohort", cohort.Id.ToString(),
                    admins.Select(id => (id, "Lớp có giao dịch cần đối soát trước khi dời lịch",
                        $"Lớp {cohort.Name} có {e.Blockers.Count} giao dịch đang giữ chỗ/chờ đối soát/đang xem xét. Yêu cầu #{request.Id} chỉ được duyệt sau khi đối soát xong.",
                        "CohortPaymentReconciliation", (string?)link)));
            await uow.SaveChangesAsync(token);
            var saved = await ScheduleRequestDetails.AsNoTracking().SingleAsync(x => x.Id == request.Id, token);
            result = ScheduleRequestView(saved, CohortSchedulePreview(e, Actor.Role == Role.Admin), idempotent: false);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> CohortScheduleRequestsAsync(int cohortId, CancellationToken ct) => Run(async () =>
    {
        await EnsureCohortScheduleActorAsync(cohortId, ct);
        var rows = await ScheduleRequestDetails.AsNoTracking().Where(x => x.CohortId == cohortId)
            .OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        var pending = rows.FirstOrDefault(x => x.Status == CohortScheduleRequest.Pending);
        object? live = null;
        if (pending is not null)
        {
            var e = await EvaluateCohortScheduleAsync(cohortId, pending.Kind, pending.ShiftWeeks, ct);
            if (e.Cohort is not null) live = CohortSchedulePreview(e, Actor.Role == Role.Admin);
        }
        return rows.Select(x => ScheduleRequestView(x, x.Id == pending?.Id ? live : null)).ToList();
    });

    public Task<ApiResponse<object>> CohortScheduleRequestQueueAsync(string? status, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var query = ScheduleRequestDetails.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var rows = await query.OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        var result = new List<object>(rows.Count);
        foreach (var x in rows)
        {
            object? live = null;
            if (x.Status == CohortScheduleRequest.Pending)
            {
                var e = await EvaluateCohortScheduleAsync(x.CohortId, x.Kind, x.ShiftWeeks, ct);
                if (e.Cohort is not null) live = CohortSchedulePreview(e, includeCourseIds: true);
            }
            result.Add(ScheduleRequestView(x, live));
        }
        return result;
    });

    public Task<ApiResponse<object>> ApproveCohortScheduleRequestAsync(int requestId, ApproveCohortScheduleRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var note = string.IsNullOrWhiteSpace(r?.Note) ? null : r!.Note!.Trim();
        Rule(note is null || note.Length <= 1000, "COHORT_SCHEDULE_NOTE_TOO_LONG", "Ghi chú tối đa 1000 ký tự.");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortScheduleRequests.Query().AsNoTracking().Where(x => x.Id == requestId)
                .Select(x => new { x.Id, x.CohortId }).SingleOrDefaultAsync(token);
            Rule(header is not null, "COHORT_SCHEDULE_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu dời/hủy lớp.", HttpStatusCode.NotFound);
            // Khóa lớp trước (tuần tự hóa mọi quyết định trên lớp), sau đó khóa lịch Mentor như luồng tạo lớp/đặt lịch.
            await uow.StudyCourses.LockCohortAsync(header!.CohortId, token);
            var request = await uow.CohortScheduleRequests.Query().Include(x => x.History).SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortScheduleRequest.Approved)
            {
                result = new { request.Id, request.Status, request.CohortId, request.Kind, request.ShiftWeeks, Idempotent = true };
                return;
            }
            Rule(request.Status == CohortScheduleRequest.Pending, "COHORT_SCHEDULE_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            Rule(r?.ExpectedRevision is null || r.ExpectedRevision == request.Revision, "COHORT_SCHEDULE_REQUEST_STALE",
                "Yêu cầu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict);
            var mentorId = await uow.StudyCohorts.Query().AsNoTracking().Where(x => x.Id == request.CohortId)
                .Select(x => x.MentorId).SingleAsync(token);
            await uow.StudyCourses.LockScheduleAsync(mentorId, mentorId, token);

            var e = await EvaluateCohortScheduleAsync(request.CohortId, request.Kind, request.ShiftWeeks, token);
            var failure = e.FirstFailure;
            if (failure is not null) throw new CourseRuleException(failure.Message, (HttpStatusCode)failure.Status, failure.Code);
            var cohort = e.Cohort!;
            var now = DateTime.UtcNow;
            string detail, title, message;
            if (request.Kind == CohortScheduleRequest.KindShiftWeeks)
            {
                var weeks = request.ShiftWeeks!.Value;
                var days = 7 * weeks;
                var enrollments = await uow.StudyCourses.Query().Include(x => x.Sessions).Include(x => x.Audit)
                    .Where(x => x.CohortId == cohort.Id).ToListAsync(token);
                var openEnrollments = enrollments.Where(x => x.Status is "Active" or "PendingPayment" or "Ready").ToList();
                var countBefore = cohort.Sessions.Count;
                foreach (var shift in e.Shifted)
                {
                    var session = shift.Session;
                    var old = session.Availability;
                    // Bản sao khung giờ: cùng Mentor, cùng snapshot chính sách/thời lượng/sức chứa; phòng họp mới.
                    var moved = new MentorAvailability
                    {
                        MentorId = old.MentorId, StartTime = shift.ProposedStart, EndTime = shift.ProposedEnd, RoomId = Guid.NewGuid(),
                        AttendanceTrackingEnabled = old.AttendanceTrackingEnabled, MaxLearners = old.MaxLearners, BookedCount = 0,
                        IsApproved = true, AdminName = "Quản trị viên (dời lịch lớp)", PublishedAt = now,
                        GroupPolicyId = old.GroupPolicyId, PolicyVersion = old.PolicyVersion,
                        SessionMinutesSnapshot = old.SessionMinutesSnapshot, RestMinutesSnapshot = old.RestMinutesSnapshot,
                        MentorScheduleRuleId = old.MentorScheduleRuleId,
                        LocalBusinessDate = old.LocalBusinessDate is null ? null : DateOnly.FromDateTime(LocalTime(shift.ProposedStart)),
                        GenerationKey = old.GenerationKey is null ? null : $"cohort-shift:{old.Id}:{request.Id}",
                        ChangeResolution = $"Buổi {session.Number} của lớp #{cohort.Id} dời {weeks} tuần từ khung giờ #{old.Id} (yêu cầu #{request.Id}).",
                        CreatedDate = now
                    };
                    old.IsDeleted = true; old.ModifiedDate = now;
                    old.ChangeResolution = $"Dời {weeks} tuần sang khung giờ mới theo yêu cầu #{request.Id} (lớp #{cohort.Id}).";
                    session.Availability = moved;
                    foreach (var learnerSession in openEnrollments.SelectMany(x => x.Sessions)
                                 .Where(x => x.AvailabilityId == old.Id && x.Status == "Scheduled"))
                    {
                        learnerSession.Availability = moved;
                        learnerSession.ScheduledAt = shift.ProposedStart;
                        learnerSession.MeetingUrl = $"/meeting-room/{moved.RoomId}";
                    }
                }
                foreach (var enrollment in openEnrollments)
                {
                    enrollment.StartsAt = enrollment.StartsAt?.AddDays(days);
                    enrollment.EndsAt = enrollment.EndsAt?.AddDays(days);
                    Audit(enrollment, "CohortScheduleShifted", $"Lịch lớp #{cohort.Id} dời {weeks} tuần theo yêu cầu #{request.Id}.");
                }
                cohort.StartsAt = cohort.StartsAt.AddDays(days);
                cohort.EndsAt = cohort.EndsAt.AddDays(days);
                Rule(cohort.Sessions.Count == countBefore, "COHORT_SHIFT_INVARIANT", "Số buổi của lớp bị thay đổi ngoài dự kiến.", HttpStatusCode.Conflict);
                detail = $"Quản trị viên đã phê duyệt dời toàn bộ {countBefore} buổi thêm {weeks} tuần; ngày bắt đầu mới {cohort.StartsAt:O}.";
                title = "Lịch lớp đã được dời";
                message = $"Lớp {cohort.Name} được dời {weeks} tuần; buổi đầu tiên mới bắt đầu {LocalTime(cohort.StartsAt):HH:mm dd/MM/yyyy}.";
            }
            else
            {
                // Lớp trống bị hủy: khung giờ được giữ làm lịch sử; lớp đã hủy không còn giữ lịch dạy của Mentor.
                cohort.Status = "Cancelled";
                detail = "Quản trị viên đã phê duyệt hủy lớp chưa có học viên.";
                title = "Lớp đã được hủy";
                message = $"Lớp {cohort.Name} chưa có học viên đã được hủy theo yêu cầu #{request.Id}.";
            }
            cohort.Revision++;
            request.Status = CohortScheduleRequest.Approved;
            request.ReviewedById = Actor.Id; request.ReviewedAtUtc = now; request.DecisionNote = note;
            request.Revision++;
            AddScheduleRequestEvent(request, "Approved", note is null ? detail : $"{detail} Ghi chú: {note}");
            await uow.SaveChangesAsync(token);

            var admins = await ActiveAdminIdsAsync(token);
            var recipients = new List<(int, string, string, string, string?)>
            {
                (cohort.MentorId, title, message, "CohortScheduleRequest", $"/mentor/cohorts/{cohort.Id}")
            };
            if (request.RequestedById != cohort.MentorId)
                recipients.Add((request.RequestedById, title, message, "CohortScheduleRequest", $"/admin/study-courses?cohortScheduleRequestId={request.Id}"));
            recipients.AddRange(admins.Select(id => (id, title, message, "CohortScheduleRequest", (string?)$"/admin/study-courses?cohortScheduleRequestId={request.Id}")));
            QueueNotifications($"cohort-schedule:{request.Id}:approved", "CohortScheduleRequest", request.Id.ToString(), recipients);
            await uow.SaveChangesAsync(token);
            result = new
            {
                request.Id, request.Status, request.CohortId, request.Kind, request.ShiftWeeks,
                CohortStatus = cohort.Status, CohortRevision = cohort.Revision, cohort.StartsAt, cohort.EndsAt,
                SessionCount = cohort.Sessions.Count, Idempotent = false
            };
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> RejectCohortScheduleRequestAsync(int requestId, RejectCohortScheduleRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var reason = r?.Reason?.Trim() ?? "";
        Rule(reason.Length is >= 1 and <= 1000, "COHORT_SCHEDULE_REQUEST_REASON_REQUIRED", "Nhập lý do từ chối (tối đa 1000 ký tự).");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortScheduleRequests.Query().AsNoTracking().Where(x => x.Id == requestId)
                .Select(x => new { x.Id, x.CohortId }).SingleOrDefaultAsync(token);
            Rule(header is not null, "COHORT_SCHEDULE_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu dời/hủy lớp.", HttpStatusCode.NotFound);
            await uow.StudyCourses.LockCohortAsync(header!.CohortId, token);
            var request = await uow.CohortScheduleRequests.Query().Include(x => x.Cohort).Include(x => x.History)
                .SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortScheduleRequest.Rejected)
            {
                result = new { request.Id, request.Status, request.DecisionNote, Idempotent = true };
                return;
            }
            Rule(request.Status == CohortScheduleRequest.Pending, "COHORT_SCHEDULE_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            Rule(r!.ExpectedRevision is null || r.ExpectedRevision == request.Revision, "COHORT_SCHEDULE_REQUEST_STALE",
                "Yêu cầu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict);
            request.Status = CohortScheduleRequest.Rejected; request.DecisionNote = reason;
            request.ReviewedById = Actor.Id; request.ReviewedAtUtc = DateTime.UtcNow; request.Revision++;
            AddScheduleRequestEvent(request, "Rejected", reason);
            QueueNotifications($"cohort-schedule:{request.Id}:rejected", "CohortScheduleRequest", request.Id.ToString(),
            [
                (request.RequestedById, "Yêu cầu dời/hủy lớp bị từ chối", reason, "CohortScheduleRequest", $"/mentor/cohorts/{request.CohortId}"),
                (request.Cohort.MentorId, "Yêu cầu dời/hủy lớp bị từ chối", reason, "CohortScheduleRequest", $"/mentor/cohorts/{request.CohortId}")
            ]);
            await uow.SaveChangesAsync(token);
            result = new { request.Id, request.Status, request.DecisionNote, Idempotent = false };
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> WithdrawCohortScheduleRequestAsync(int requestId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Mentor)) throw new UnauthorizedAccessException();
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var header = await uow.CohortScheduleRequests.Query().AsNoTracking().Where(x => x.Id == requestId)
                .Select(x => new { x.Id, x.CohortId, x.RequestedById }).SingleOrDefaultAsync(token);
            Rule(header is not null, "COHORT_SCHEDULE_REQUEST_NOT_FOUND", "Không tìm thấy yêu cầu dời/hủy lớp.", HttpStatusCode.NotFound);
            if (header!.RequestedById != Actor.Id) throw new UnauthorizedAccessException();
            await uow.StudyCourses.LockCohortAsync(header.CohortId, token);
            var request = await uow.CohortScheduleRequests.Query().Include(x => x.History).SingleAsync(x => x.Id == requestId, token);
            if (request.Status == CohortScheduleRequest.Withdrawn) { result = new { request.Id, request.Status, Idempotent = true }; return; }
            Rule(request.Status == CohortScheduleRequest.Pending, "COHORT_SCHEDULE_REQUEST_NOT_PENDING", "Yêu cầu đã được xử lý.", HttpStatusCode.Conflict);
            request.Status = CohortScheduleRequest.Withdrawn; request.Revision++;
            AddScheduleRequestEvent(request, "Withdrawn", "Người gửi rút yêu cầu.");
            await uow.SaveChangesAsync(token);
            result = new { request.Id, request.Status, Idempotent = false };
        }, ct);
        return result;
    });
}
