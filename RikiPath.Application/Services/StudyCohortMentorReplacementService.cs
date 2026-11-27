using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

// Admin thay Mentor chính cho phần còn lại của lớp (khác với Mentor dạy thay một buổi riêng lẻ).
// Buổi đã diễn ra/đang diễn ra giữ nguyên khung giờ và Mentor thực dạy; chỉ các buổi tương lai được chuyển.
public sealed partial class StudyCourseService
{
    private sealed record ReplacementEvaluation(StudyCohort? Cohort, UserAccount? Mentor, MentorGroupPolicy Policy, int FromSessionNumber,
        List<StudyCohortSession> Retained, List<StudyCohortSession> Affected, List<TransferCheck> Checks, List<object> Conflicts)
    {
        public bool Eligible => Checks.All(c => c.Passed);
        public TransferCheck? FirstFailure => Checks.FirstOrDefault(c => !c.Passed);
    }

    private async Task<ReplacementEvaluation> EvaluateReplacementAsync(int cohortId, int mentorId, int? fromSessionNumber, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var policy = await CurrentPolicyAsync(ct);
        var checks = new List<TransferCheck>();
        var conflicts = new List<object>();
        void Check(string code, bool passed, string message, HttpStatusCode status = HttpStatusCode.UnprocessableEntity)
            => checks.Add(new TransferCheck(code, passed, message, (int)status));

        Check("REPLACEMENT_DISABLED_BY_POLICY", policy.MentorReplacementEnabled, "Chính sách hiện hành chưa cho phép thay Giáo viên phụ trách.");
        var cohort = await CohortForTransfer.SingleOrDefaultAsync(x => x.Id == cohortId, ct);
        Check("REPLACEMENT_COHORT_NOT_FOUND", cohort is not null, "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        if (cohort is null) return new ReplacementEvaluation(null, null, policy, 0, [], [], checks, conflicts);
        Check("REPLACEMENT_COHORT_NOT_ACTIVE", cohort.Status is "Published" or "Draft", "Lớp đã đóng; không thể thay Giáo viên.", HttpStatusCode.Conflict);
        Check("REPLACEMENT_SAME_MENTOR", cohort.MentorId != mentorId, "Giáo viên thay thế phải khác Giáo viên hiện tại.");

        var mentor = await uow.UserAccounts.Query().Include(x => x.MentorProfile).SingleOrDefaultAsync(x => x.Id == mentorId, ct);
        Check("REPLACEMENT_MENTOR_NOT_ELIGIBLE", mentor is { Role: Role.Mentor, IsDeleted: false }
                && mentor.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved,
            "Giáo viên thay thế chưa được phê duyệt hồ sơ hoặc không còn hoạt động.");
        var qualCodes = uow.MentorTeachingQualifications is null
            ? new List<string>()
            : await uow.MentorTeachingQualifications.Query().AsNoTracking()
                .Include(q => q.CertificateLevel)
                .Where(q => q.MentorUserId == mentorId && q.IsActive && q.RevokedAtUtc == null)
                .Select(q => q.CertificateLevel.Code)
                .ToListAsync(ct);
        var highestRank = qualCodes
            .Append(mentor?.MentorProfile?.JlptLevel)
            .Select(RikiPath.Domain.MentorScheduling.MentorSlotPolicy.GetJlptRank)
            .DefaultIfEmpty(0)
            .Max();
        var targetLevelCode = cohort.Plan.CertificateLevel?.Code;
        var hasDirectQualification = uow.MentorTeachingQualifications is not null && await uow.MentorTeachingQualifications.Query()
            .AnyAsync(q => q.MentorUserId == mentorId && q.CertificateLevelId == cohort.Plan.CertificateLevelId && q.IsActive && q.RevokedAtUtc == null, ct);
        var qualified = hasDirectQualification || RikiPath.Domain.MentorScheduling.MentorSlotPolicy.CanMentorTeachLevel(highestRank, targetLevelCode);
        Check("REPLACEMENT_LEVEL_NOT_QUALIFIED", qualified, $"Giáo viên thay thế chưa đủ trình độ hoặc chưa được phân quyền giảng dạy cấp độ {targetLevelCode}.");

        if (mentor is not null)
        {
            var levelLimit = policy.GetMaxActiveClassesForLevel(cohort.Plan.CertificateLevelId, targetLevelCode);
            var maxTotalLimit = Math.Max(1, policy.MaxTotalActiveClassesPerMentor);
            var targetCohorts = await uow.StudyCohorts.Query().AsNoTracking()
                .Include(c => c.Plan)
                .Include(c => c.Enrollments)
                .Where(c => c.MentorId == mentorId && c.Id != cohort.Id)
                .Select(c => new
                {
                    c.Id,
                    c.Status,
                    c.EndsAt,
                    c.SourceProposalId,
                    LevelId = c.Plan.CertificateLevelId,
                    HasConfirmed = c.Enrollments.Any(e =>
                        (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                        && e.Status != "PaymentReview"
                        && e.Status != "Cancelled"
                        && e.Status != "Transferred"
                        && e.Status != "PendingPayment"
                        && e.Status != "PaymentExpired"
                        && e.Status != "PaymentFailed"),
                    HasIncompleteConfirmed = c.Enrollments.Any(e =>
                        (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                        && e.Status != "PaymentReview"
                        && e.Status != "Cancelled"
                        && e.Status != "Transferred"
                        && e.Status != "PendingPayment"
                        && e.Status != "PaymentExpired"
                        && e.Status != "PaymentFailed"
                        && e.Status != "Completed"),
                })
                .ToListAsync(ct);

            var activeTargetCohorts = targetCohorts
                .Where(c => !string.Equals(c.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(c.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    && (!(c.HasConfirmed && !c.HasIncompleteConfirmed))
                    && (c.HasConfirmed || c.EndsAt > now))
                .ToList();

            var cohortProposalIds = targetCohorts
                .Where(c => c.SourceProposalId.HasValue)
                .Select(c => c.SourceProposalId!.Value)
                .ToHashSet();

            var activeTargetProposals = await uow.MentorTeachingProposals.Query().AsNoTracking()
                .Where(p => p.MentorId == mentorId
                    && p.CertificateLevelId.HasValue
                    && (p.Status == MentorTeachingProposalStatus.PendingReview
                        || p.Status == MentorTeachingProposalStatus.ChangesRequested
                        || p.Status == MentorTeachingProposalStatus.Approved))
                .Select(p => new
                {
                    p.Id,
                    LevelId = p.CertificateLevelId!.Value,
                    p.Status,
                    HasFutureSlots = p.GeneratedAvailabilities.Any(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null && a.EndTime > now),
                })
                .ToListAsync(ct);

            var unlinkedActiveProposals = activeTargetProposals
                .Where(p => p.Status != MentorTeachingProposalStatus.Approved
                    || (!cohortProposalIds.Contains(p.Id) && p.HasFutureSlots))
                .ToList();

            var activeAtTargetLevel = activeTargetCohorts.Count(c => c.LevelId == cohort.Plan.CertificateLevelId)
                + unlinkedActiveProposals.Count(p => p.LevelId == cohort.Plan.CertificateLevelId);
            var totalActiveForMentor = activeTargetCohorts.Count + unlinkedActiveProposals.Count;

            Check("REPLACEMENT_LEVEL_CLASS_LIMIT", levelLimit > 0 && activeAtTargetLevel < levelLimit,
                levelLimit <= 0
                    ? $"Cấp độ JLPT {targetLevelCode} hiện tạm ngưng nhận lớp mới theo cấu hình."
                    : $"Giáo viên thay thế đã đạt giới hạn tối đa {levelLimit} lớp đồng thời ở cấp độ JLPT {targetLevelCode}.",
                HttpStatusCode.Conflict);
            Check("REPLACEMENT_TOTAL_CLASS_LIMIT", totalActiveForMentor < maxTotalLimit,
                $"Giáo viên thay thế đã đạt tổng giới hạn tối đa {maxTotalLimit} lớp đồng thời của hệ thống.",
                HttpStatusCode.Conflict);
        }

        var earliest = now.AddHours(policy.MentorReplacementLeadHours);
        var ordered = cohort.Sessions.OrderBy(s => s.Number).ToList();
        var from = fromSessionNumber ?? ordered.FirstOrDefault(s => s.Availability.StartTime >= earliest)?.Number ?? 0;
        var affected = ordered.Where(s => from > 0 && s.Number >= from).ToList();
        var retained = ordered.Where(s => from <= 0 || s.Number < from).ToList();
        Check("REPLACEMENT_NO_FUTURE_SESSIONS", affected.Count > 0, "Lớp không còn buổi học sắp tới đáp ứng đủ thời gian báo trước để chuyển cho Giáo viên mới.", HttpStatusCode.Conflict);
        Check("REPLACEMENT_LEAD_TIME", affected.All(s => s.Availability.StartTime >= earliest),
            $"Buổi học đầu tiên chuyển cho Giáo viên mới phải bắt đầu sau ít nhất {policy.MentorReplacementLeadHours} giờ; các buổi đã hoặc sắp diễn ra vẫn do Giáo viên hiện tại phụ trách.");
        Check("REPLACEMENT_SESSION_LOCKED", affected.All(s => s.Availability.IsApproved && !s.Availability.IsDeleted
                && s.Availability.RejectedAt == null && s.Availability.ChangeRequestedAt == null && s.Availability.MentorId == cohort.MentorId),
            "Có buổi học đang chờ điều chỉnh lịch hoặc không còn thuộc Giáo viên hiện tại.", HttpStatusCode.Conflict);
        var affectedIds = affected.Select(s => s.AvailabilityId).ToList();
        Check("REPLACEMENT_PENDING_CHANGE_REQUEST", !await uow.CourseScheduleRequests.Query().AnyAsync(x => x.Status == "Pending"
                && x.Course.CohortId == cohortId && x.Course.Sessions.Any(s => s.Id == x.SessionId && s.AvailabilityId.HasValue && affectedIds.Contains(s.AvailabilityId.Value)), ct),
            "Có yêu cầu dời/hủy buổi học đang chờ xử lý trong phạm vi thay Giáo viên.", HttpStatusCode.Conflict);

        if (mentor is not null && affected.Count > 0)
        {
            // Kiểm tra chuẩn cho Mentor thay thế: chỉ cam kết dạy thật (lớp đã công bố, lịch 1-1 đã xác nhận, buổi khóa 1-1)
            // là xung đột; khung giờ rảnh chưa có người đặt không còn bị tính.
            var report = await TeachingConflicts.CheckAsync(mentorId,
                affected.Select(s => new ProposedTeachingInterval(s.Availability.StartTime, s.Availability.EndTime, s.Number)).ToList(),
                new TeachingConflictOptions
                {
                    ExcludeCohortId = cohort.Id,
                    RestMinutes = policy.Version == 0 ? null : policy.RestMinutes,
                    DailyCap = policy.Version == 0 ? null : policy.DailySessionCap,
                    WeeklyCap = policy.Version == 0 ? null : policy.WeeklySessionCap,
                    // Các buổi của chính lớp đã được xếp hợp lệ; chỉ xét va chạm với lịch hiện có của Mentor mới.
                    IncludeOwnIntervals = false
                }, ct);
            var capCodes = new[] { MentorTeachingConflictService.DailyLimit, MentorTeachingConflictService.WeeklyLimit };
            var scheduleConflicts = report.Conflicts.Where(x => !capCodes.Contains(x.Code)).ToList();
            var capConflicts = report.Conflicts.Where(x => capCodes.Contains(x.Code)).ToList();
            foreach (var x in report.Conflicts)
                conflicts.Add(new
                {
                    x.SessionNumber, Kind = x.CommitmentKind, x.Code, x.CohortId, x.CohortName,
                    StartTime = x.CommitmentStartUtc ?? x.ProposedStartUtc, EndTime = x.CommitmentEndUtc ?? x.ProposedEndUtc, x.Message
                });
            Check("REPLACEMENT_MENTOR_SCHEDULE_CONFLICT", scheduleConflicts.Count == 0,
                scheduleConflicts.Count == 0 ? "Giáo viên thay thế không bị trùng lịch dạy."
                    : $"Giáo viên thay thế bị trùng lịch hoặc không đủ thời gian nghỉ ở buổi {scheduleConflicts[0].SessionNumber}: {scheduleConflicts[0].Message}",
                HttpStatusCode.Conflict);
            Check("REPLACEMENT_DAILY_CAP", capConflicts.Count == 0,
                capConflicts.Count == 0 ? "Giáo viên thay thế không vượt giới hạn số buổi dạy."
                    : $"Giáo viên thay thế sẽ vượt số buổi tối đa theo chính sách: {capConflicts[0].Message}", HttpStatusCode.Conflict);
        }
        return new ReplacementEvaluation(cohort, mentor, policy, from, retained, affected, checks, conflicts);
    }

    private static object ReplacementSession(StudyCohortSession s) => new
    {
        s.Number, s.AvailabilityId, s.Availability.StartTime, s.Availability.EndTime, ActualMentorId = s.Availability.MentorId
    };

    public Task<ApiResponse<object>> PreviewMentorReplacementAsync(int cohortId, int mentorId, int? fromSessionNumber, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var e = await EvaluateReplacementAsync(cohortId, mentorId, fromSessionNumber, ct);
        Rule(e.Cohort is not null, "REPLACEMENT_COHORT_NOT_FOUND", "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        var now = DateTime.UtcNow;
        var history = await uow.StudyCohortMentorAssignments.Query().AsNoTracking().Where(x => x.CohortId == cohortId)
            .OrderByDescending(x => x.Id).ToListAsync(ct);
        var activeLearners = e.Cohort!.Enrollments.Count(x => x.Status == "Active" && x.PaymentStatus is "Paid" or "PartiallyRefunded");
        return new
        {
            CohortId = e.Cohort.Id, e.Cohort.Name, e.Cohort.Revision, e.Cohort.Capacity,
            CurrentMentorId = e.Cohort.MentorId, CurrentMentorName = $"{e.Cohort.Mentor?.FirstName} {e.Cohort.Mentor?.LastName}".Trim(),
            ReplacementMentorId = mentorId, ReplacementMentorName = e.Mentor is null ? null : $"{e.Mentor.FirstName} {e.Mentor.LastName}".Trim(),
            Level = e.Cohort.Plan.CertificateLevel?.Code, PolicyVersion = e.Policy.Version, LeadHours = e.Policy.MentorReplacementLeadHours,
            e.FromSessionNumber, e.Eligible,
            Impact = new
            {
                RetainedSessionCount = e.Retained.Count, AffectedSessionCount = e.Affected.Count,
                ActiveLearnerCount = activeLearners, EnrolledCount = e.Cohort.Enrollments.Count(x => ReservesSeat(x, now)),
                CapacityUnchanged = true, EnrollmentsUnchanged = true
            },
            RetainedSessions = e.Retained.Select(ReplacementSession),
            AffectedSessions = e.Affected.Select(ReplacementSession),
            e.Conflicts,
            Checks = e.Checks.Select(c => new { c.Code, c.Passed, c.Message }),
            History = history.Select(h => new { h.Id, h.PreviousMentorId, h.MentorId, h.EffectiveFromSessionNumber, h.EffectiveFromUtc, h.AffectedSessionCount, h.Reason, h.AssignedById, h.CreatedAtUtc })
        };
    });

    public Task<ApiResponse<object>> MentorAssignmentsAsync(int cohortId, CancellationToken ct) => Run(async () =>
    {
        var cohort = await uow.StudyCohorts.Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == cohortId, ct);
        Rule(cohort is not null, "REPLACEMENT_COHORT_NOT_FOUND", "Không tìm thấy lớp.", HttpStatusCode.NotFound);
        var rows = await uow.StudyCohortMentorAssignments.Query().AsNoTracking().Where(x => x.CohortId == cohortId).OrderBy(x => x.Id).ToListAsync(ct);
        if (Actor.Role != Role.Admin && !(Actor.Role == Role.Mentor
            && (cohort!.MentorId == Actor.Id || rows.Any(x => x.MentorId == Actor.Id || x.PreviousMentorId == Actor.Id))))
            throw new UnauthorizedAccessException();
        return rows.Select(h => new
        {
            h.Id, h.PreviousMentorId, h.MentorId, h.EffectiveFromSessionNumber, h.EffectiveFromUtc, h.AffectedSessionCount,
            Reason = Actor.Role == Role.Admin ? h.Reason : null, h.AssignedById, h.PolicyVersionSnapshot, h.CreatedAtUtc
        }).ToList();
    });

    public Task<ApiResponse<object>> ReplaceCohortMentorAsync(int cohortId, ReplaceCohortMentorRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(r.IdempotencyKey != Guid.Empty, "REPLACEMENT_IDEMPOTENCY_KEY_REQUIRED", "Thiếu mã chống gửi trùng.");
        var reason = r.Reason?.Trim() ?? "";
        Rule(reason.Length is >= 10 and <= 1000, "REPLACEMENT_REASON_REQUIRED", "Vui lòng nhập lý do thay Giáo viên từ 10 đến 1000 ký tự.");
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var existing = await uow.StudyCohortMentorAssignments.Query().AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == r.IdempotencyKey, token);
            if (existing is not null)
            {
                Rule(existing.CohortId == cohortId && existing.MentorId == r.MentorId, "REPLACEMENT_IDEMPOTENCY_KEY_REUSED",
                    "Mã chống gửi trùng đã dùng cho thao tác khác.", HttpStatusCode.Conflict);
                result = new { AssignmentId = existing.Id, existing.CohortId, existing.MentorId, existing.EffectiveFromSessionNumber, existing.AffectedSessionCount, Idempotent = true };
                return;
            }
            await uow.StudyCourses.LockCohortAsync(cohortId, token);
            var currentMentorId = await uow.StudyCohorts.Query().AsNoTracking().Where(x => x.Id == cohortId).Select(x => (int?)x.MentorId).SingleOrDefaultAsync(token);
            Rule(currentMentorId.HasValue, "REPLACEMENT_COHORT_NOT_FOUND", "Không tìm thấy lớp.", HttpStatusCode.NotFound);
            // Cùng khóa lịch với luồng tạo lớp/đặt lịch để không ai chen vào lịch của hai Mentor trong lúc chuyển.
            await uow.StudyCourses.LockScheduleAsync(r.MentorId, currentMentorId!.Value, token);
            var e = await EvaluateReplacementAsync(cohortId, r.MentorId, r.FromSessionNumber, token);
            var failure = e.FirstFailure;
            if (failure is not null) throw new CourseRuleException(failure.Message, (HttpStatusCode)failure.Status, failure.Code);
            var cohort = e.Cohort!;
            Rule(cohort.Revision == r.ExpectedCohortRevision, "REPLACEMENT_COHORT_STALE", "Lớp vừa thay đổi; hãy xem lại tác động trước khi xác nhận.", HttpStatusCode.Conflict);
            var now = DateTime.UtcNow;
            var previousMentorId = cohort.MentorId;
            var capacityBefore = cohort.Capacity;
            var enrollmentIds = cohort.Enrollments.Select(x => x.Id).OrderBy(x => x).ToList();
            var assignment = new StudyCohortMentorAssignment
            {
                CohortId = cohort.Id, PreviousMentorId = previousMentorId, MentorId = r.MentorId,
                EffectiveFromSessionNumber = e.FromSessionNumber, EffectiveFromUtc = e.Affected.Min(s => s.Availability.StartTime),
                AffectedSessionCount = e.Affected.Count, Reason = reason, AssignedById = Actor.Id, IdempotencyKey = r.IdempotencyKey,
                PolicyId = e.Policy.Id == 0 ? null : e.Policy.Id, PolicyVersionSnapshot = e.Policy.Version == 0 ? null : e.Policy.Version,
                CreatedAtUtc = now,
                ImpactSnapshot = JsonSerializer.Serialize(new
                {
                    Retained = e.Retained.Select(s => new { s.Number, s.AvailabilityId, s.Availability.MentorId }),
                    Affected = e.Affected.Select(s => new { s.Number, FromAvailabilityId = s.AvailabilityId })
                })
            };
            await uow.StudyCohortMentorAssignments.AddAsync(assignment, token);

            var enrollments = await uow.StudyCourses.Query().Include(x => x.Sessions).Include(x => x.Audit)
                .Where(x => x.CohortId == cohort.Id).ToListAsync(token);
            foreach (var session in e.Affected)
            {
                var old = session.Availability;
                // Khung giờ mới thuộc Mentor thay thế, giữ nguyên giờ/sức chứa/snapshot chính sách; phòng họp mới.
                var replacement = new MentorAvailability
                {
                    MentorId = r.MentorId, StartTime = old.StartTime, EndTime = old.EndTime, RoomId = Guid.NewGuid(),
                    AttendanceTrackingEnabled = old.AttendanceTrackingEnabled, MaxLearners = old.MaxLearners, BookedCount = 0,
                    IsApproved = true, AdminName = "Quản trị viên (thay Giáo viên phụ trách)", PublishedAt = now,
                    GroupPolicyId = old.GroupPolicyId, PolicyVersion = old.PolicyVersion,
                    SessionMinutesSnapshot = old.SessionMinutesSnapshot, RestMinutesSnapshot = old.RestMinutesSnapshot,
                    LocalBusinessDate = old.LocalBusinessDate, MentorScheduleRuleId = old.MentorScheduleRuleId,
                    GenerationKey = old.GenerationKey is null ? null : $"mentor-replacement:{old.Id}:{r.MentorId}",
                    ChangeResolution = $"Nhận buổi {session.Number} của lớp {cohort.Name}.",
                    CreatedDate = now
                };
                old.IsDeleted = true; old.ModifiedDate = now;
                old.ChangeResolution = $"Chuyển cho Giáo viên mới (thay Giáo viên phụ trách lớp {cohort.Name}).";
                session.Availability = replacement;
                foreach (var learnerSession in enrollments.Where(x => x.Status == "Active" && x.PaymentStatus is "Paid" or "PartiallyRefunded")
                    .SelectMany(x => x.Sessions).Where(x => x.AvailabilityId == old.Id && x.Status == "Scheduled"))
                {
                    learnerSession.Availability = replacement;
                    learnerSession.MeetingUrl = $"/meeting-room/{replacement.RoomId}";
                }
            }
            foreach (var enrollment in enrollments.Where(x => x.Status is "Active" or "PendingPayment" or "Ready"))
            {
                // Mentor phụ trách hiện tại của ghi danh đang học; buổi đã dạy vẫn gắn khung giờ của Mentor cũ.
                enrollment.MentorId = r.MentorId;
                Audit(enrollment, "CohortMentorReplaced", $"Thay Giáo viên phụ trách từ buổi {e.FromSessionNumber}. {reason}");
            }
            cohort.MentorId = r.MentorId;
            cohort.Revision++;
            await uow.SaveChangesAsync(token);
            Rule(cohort.Capacity == capacityBefore && cohort.Enrollments.Select(x => x.Id).OrderBy(x => x).SequenceEqual(enrollmentIds),
                "REPLACEMENT_INVARIANT", "Sĩ số hoặc ghi danh bị thay đổi ngoài dự kiến.", HttpStatusCode.Conflict);
            var learnerIds = enrollments.Where(x => x.Status == "Active" && x.PaymentStatus is "Paid" or "PartiallyRefunded").Select(x => x.LearnerId).ToList();
            QueueNotifications($"cohort-mentor-replacement:{assignment.Id}", "StudyCohort", cohort.Id.ToString(),
                learnerIds.Select(id => (id, "Lớp học có Giáo viên mới", $"Từ buổi {e.FromSessionNumber}, lớp {cohort.Name} sẽ do Giáo viên mới phụ trách.", "CohortMentorReplaced", (string?)"/my-courses"))
                    .Append((r.MentorId, "Bạn được phân công lớp mới", $"Bạn phụ trách lớp {cohort.Name} từ buổi {e.FromSessionNumber}.", "CohortMentorReplaced", (string?)$"/mentor/cohorts/{cohort.Id}"))
                    .Append((previousMentorId, "Thay đổi phân công lớp", $"Lớp {cohort.Name} đã được chuyển cho Giáo viên khác phụ trách từ buổi {e.FromSessionNumber}.", "CohortMentorReplaced", (string?)"/mentor/cohorts")));
            await uow.SaveChangesAsync(token);
            result = new { AssignmentId = assignment.Id, CohortId = cohort.Id, assignment.MentorId, assignment.PreviousMentorId,
                assignment.EffectiveFromSessionNumber, assignment.AffectedSessionCount, CohortRevision = cohort.Revision, Idempotent = false };
        }, ct);
        return result;
    });
}
