using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class HomeworkService
{
    private const int MockPassPercent = 70;

    private sealed record ExamResult(int AttemptId, string Kind, DateTime SubmittedAt, decimal Percent,
        List<SectionResultItem> Sections);
    private sealed record PracticeOutcome(ExamResult? Before, ExamResult After, string Decision, int MentorId,
        int AssignedCount, int SubmittedCount, DateTime RecordedAt);
    private sealed record PlanPeriod(int? LevelId, DateTime? Start, DateTime? End, string? WaitingReason,
        int AssignedCount, int SubmittedCount, int TrackedSessions, int AttendedSessions, int? LeadMentorId);

    private IQueryable<MentorBooking> BookingsFor(ClaimDTO actor) => unit.MentorBookings.Query().Where(b =>
        !b.IsDeleted && !b.UserSubscription.IsDeleted && b.UserSubscription.PaymentStatus == PaymentStatus.Paid &&
        b.MentorAvailability != null && (actor.Role == Role.Admin ||
            (actor.Role == Role.Mentor && b.MentorAvailability.MentorId == actor.Id) ||
            (actor.Role == Role.Learner && b.UserSubscription.UserId == actor.Id)));

    public async Task<object> ListBookingsAsync(CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        var now = DateTime.UtcNow;
        var reviews = unit.MentorReviews.Query();
        var rows = await BookingsFor(actor).AsNoTracking()
            .OrderByDescending(b => b.MentorAvailability!.StartTime).ThenByDescending(b => b.Id)
            .Select(b => new
            {
                BookingId = b.Id, b.UserSubscriptionId, LearnerId = b.UserSubscription.UserId,
                LearnerName = (b.UserSubscription.UserAccount.FirstName + " " + b.UserSubscription.UserAccount.LastName).Trim(),
                MentorName = (b.MentorAvailability!.Mentor.FirstName + " " + b.MentorAvailability.Mentor.LastName).Trim(),
                MentorId = b.MentorAvailability.MentorId, b.Status, b.MentorAvailability.StartTime, b.MentorAvailability.EndTime,
                b.MentorAvailability.ChangeResolution, b.MentorAvailability.ChangeRequestedAt, b.CancellationReason,
                b.PracticeScore, b.PracticeFeedback, b.PracticeFeedbackAt, b.AttendedAt,
                Review = reviews.Where(r => r.MentorBookingId == b.Id)
                    .Select(r => new { r.Id, r.Rating, r.Comment }).FirstOrDefault()
            }).ToListAsync(ct);
        var moved = await unit.MentorBookings.GetRescheduledBookingKeysAsync(rows.Select(row => row.BookingId), ct);
        return rows.Select(row => new
        {
            row.BookingId, row.UserSubscriptionId, row.LearnerId, row.LearnerName, row.MentorName, row.MentorId,
            Status = row.Status.ToString(), StartsAt = BusinessDateHelper.EnsureUtc(row.StartTime), EndsAt = BusinessDateHelper.EnsureUtc(row.EndTime),
            row.ChangeResolution, row.ChangeRequestedAt, row.CancellationReason, row.PracticeScore, row.PracticeFeedback, row.PracticeFeedbackAt,
            WasRescheduled = moved.Contains(row.BookingId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            HasEnded = row.EndTime <= now,
            Attended = row.AttendedAt.HasValue, row.Review,
        });
    }

    public async Task<ApiResponse<object>> EvaluateBookingAsync(int id, EvaluateMentorBookingRequest request, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Mentor) return ApiResponse<object>.Fail("Chỉ mentor được đánh giá buổi học.", HttpStatusCode.Forbidden);
        var feedback = request.Feedback?.Trim();
        if (request.Score is < 0 or > 100 || feedback?.Length is not (> 0 and <= 2000))
            return ApiResponse<object>.Fail("Nhập điểm từ 0 đến 100 và nhận xét tối đa 2000 ký tự.");
        var booking = await BookingsFor(actor).AsTracking().Include(b => b.UserSubscription).Include(b => b.MentorAvailability)
            .SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return ApiResponse<object>.NotFound("Không tìm thấy lịch học của mentor.");
        if (booking.Status is not (MentorStatus.Assigned or MentorStatus.Accepted or MentorStatus.Completed) ||
            booking.MentorAvailability!.EndTime > DateTime.UtcNow)
            return ApiResponse<object>.Fail("Chỉ gửi đánh giá sau khi buổi ôn luyện kết thúc.", HttpStatusCode.Conflict);
        if (booking.PracticeScore == request.Score && booking.PracticeFeedback == feedback)
            return ApiResponse<object>.Success(new { booking.Id });

        booking.PracticeScore = request.Score;
        booking.PracticeFeedback = feedback;
        booking.PracticeFeedbackAt = DateTime.UtcNow;
        await NotifyAsync(booking.UserSubscription.UserId, "Mentor đã gửi nhận xét buổi học", "Mở lịch ôn luyện để xem nhận xét.", id, ct);
        try { await unit.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<object>.Fail("Lịch vừa thay đổi. Hãy tải lại trước khi gửi đánh giá.", HttpStatusCode.Conflict);
        }
        return ApiResponse<object>.Success(new { booking.Id });
    }

    private IQueryable<MentorBooking> ProgressBooking(ClaimDTO actor, int id) => BookingsFor(actor).Where(b => b.Id == id)
        .Include(b => b.UserSubscription).ThenInclude(s => s.UserAccount);

    private async Task<PlanPeriod> GetPeriodAsync(MentorBooking anchor, CancellationToken ct)
    {
        var plan = anchor.UserSubscription;
        var now = DateTime.UtcNow;
        var bookings = await ScheduledBookings(unit).AsNoTracking().Where(b => b.UserSubscriptionId == plan.Id)
            .OrderBy(b => b.MentorAvailability!.EndTime).ThenBy(b => b.Id)
            .Select(b => new { b.Id, b.MentorAvailability!.StartTime, b.MentorAvailability.EndTime, b.MentorAvailability.MentorId,
                b.MentorAvailability.AttendanceTrackingEnabled, b.AttendedAt, b.PracticeFeedbackAt }).ToListAsync(ct);
        var homework = await unit.HomeworkAssignments.Query().Where(a => !a.IsDeleted && a.MentorBooking != null
                && a.MentorBooking.UserSubscriptionId == plan.Id && a.LearnerId == plan.UserId)
            .Select(a => new { a.DueAt, Submitted = a.Submission != null && !a.Submission.IsDeleted }).ToListAsync(ct);
        var latest = bookings.LastOrDefault();
        var paidUses = Math.Max(plan.MeetingSessionsUsed, bookings.Count);
        var reason = latest is null ? "Chưa có buổi ôn luyện trong gói."
            : latest.EndTime > now ? "Cần chờ các buổi đã đặt kết thúc."
            : paidUses < plan.MeetingSessionsIncluded && (!plan.EndDate.HasValue || plan.EndDate > now)
                ? "Gói còn lượt ôn luyện chưa dùng."
            : bookings.Any(b => b.AttendedAt.HasValue && !b.PracticeFeedbackAt.HasValue) ? "Mentor cần gửi nhận xét cho các buổi đã tham gia."
            : homework.Any(h => !h.Submitted && h.DueAt > now) ? "Vẫn còn bài tập chưa đến hạn nộp." : null;
        return new(plan.CertificateLevelId ?? anchor.CompensationLevelId ?? plan.UserAccount.TargetCertificateLevelId,
            bookings.Count > 0 ? bookings.Min(b => b.StartTime) : null, latest?.EndTime, reason,
            homework.Count, homework.Count(h => h.Submitted), bookings.Count(b => b.AttendanceTrackingEnabled && b.EndTime <= now),
            bookings.Count(b => b.AttendanceTrackingEnabled && b.EndTime <= now && b.AttendedAt.HasValue), latest?.MentorId);
    }

    private IQueryable<MockTestAttempt> CompletedExams(int learnerId) => unit.MockTestAttempts.Query().AsNoTracking()
        .Where(a => a.UserId == learnerId && a.IsCompleted && !a.IsDeleted && a.SubmittedAt.HasValue);

    private IQueryable<MockTestAttempt> FinalExams(int learnerId, int levelId, DateTime endedAt) => CompletedExams(learnerId)
        .Where(a => a.AttemptKind == AttemptKind.MockTest && a.StartedAt >= endedAt &&
            a.SectionResults!.Any(r => !r.IsDeleted && r.MockTestSection.MockTest.CertificateLevelId == levelId));

    private static IQueryable<MockTestAttempt> ExamDetails(IQueryable<MockTestAttempt> query) => query
        .Include(a => a.SectionResults!).ThenInclude(r => r.MockTestSection).ThenInclude(s => s.MockTest)
        .Include(a => a.SectionResults!).ThenInclude(r => r.MockTestSection).ThenInclude(s => s.LanguageSkill).AsSplitQuery();

    private static ExamResult? ToExamResult(MockTestAttempt attempt, int levelId)
    {
        var sections = attempt.AttemptKind == AttemptKind.Placement
            ? PracticeTestService.TryGetSkillEvidenceFromSnapshot(attempt.QuestionSnapshotJson)?.Where(s => s.JlptLevelId == levelId).ToList() : null;
        sections ??= (attempt.SectionResults ?? []).Where(r => !r.IsDeleted && r.MockTestSection.MockTest.CertificateLevelId == levelId)
            .Select(r => new SectionResultItem { MockTestSectionId = r.MockTestSectionId, SectionTitle = r.MockTestSection.Title,
                LanguageSkillName = r.MockTestSection.LanguageSkill.Name, CorrectCount = r.CorrectCount, TotalCount = r.TotalCount, Score = r.ScorePercent }).ToList();
        sections = sections.Where(s => s.TotalCount > 0 && s.CorrectCount >= 0 && s.CorrectCount <= s.TotalCount &&
            JlptSkillAssessmentPolicy.Classify(s.LanguageSkillName).HasValue).ToList();
        return sections.Count == 0 ? null : new(attempt.Id, attempt.AttemptKind.ToString(), BusinessDateHelper.EnsureUtc(attempt.SubmittedAt!.Value),
            100m * sections.Sum(s => (long)s.CorrectCount) / sections.Sum(s => (long)s.TotalCount), sections);
    }

    private async Task<ExamResult?> FindBaselineAsync(int learnerId, int levelId, DateTime start, CancellationToken ct)
    {
        var exams = CompletedExams(learnerId).Where(a => a.SubmittedAt <= start &&
            (a.AttemptKind == AttemptKind.Placement || (a.AttemptKind == AttemptKind.MockTest &&
                a.SectionResults!.Any(r => !r.IsDeleted && r.MockTestSection.MockTest.CertificateLevelId == levelId))));
        var candidates = await ExamDetails(exams.OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id)).ToListAsync(ct);
        return candidates.Select(a => ToExamResult(a, levelId)).FirstOrDefault(r => r is not null);
    }

    private static PracticeOutcome? ReadPracticeOutcome(UserSubscription plan) => plan.MentorPracticeOutcomeJson is { } json
        ? JsonSerializer.Deserialize<PracticeOutcome>(json, HomeworkJson) : null;

    public async Task<ApiResponse<object>> GetPracticeProgressAsync(int id, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        var booking = await ProgressBooking(actor, id).AsNoTracking().SingleOrDefaultAsync(ct);
        if (booking is null) return ApiResponse<object>.NotFound("Không tìm thấy lịch ôn luyện.");
        var plan = booking.UserSubscription;
        var period = await GetPeriodAsync(booking, ct);
        var outcome = ReadPracticeOutcome(plan);
        var level = period.LevelId is { } levelId ? await unit.CertificateLevels.GetByIdAsync(levelId, ct) : null;
        var waiting = period.WaitingReason ?? (level is null ? "Gói học chưa có cấp độ để đối chiếu thi thử." : null);
        var baseline = outcome is not null ? outcome.Before
            : period.Start.HasValue && level is not null ? await FindBaselineAsync(plan.UserId, level.Id, period.Start.Value, ct) : null;
        var attempts = outcome is null && actor.Role == Role.Learner && waiting is null && period.End.HasValue && level is not null
            ? await FinalExams(plan.UserId, level.Id, period.End.Value).OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id)
                .Take(40).Select(a => new { AttemptId = a.Id, a.SubmittedAt, a.TotalScore }).ToListAsync(ct) : null;
        return ApiResponse<object>.Success(new { Baseline = baseline, Outcome = outcome, Attempts = attempts, WaitingReason = waiting,
            Level = level?.Code, PassPercent = MockPassPercent, period.AssignedCount, period.SubmittedCount,
            period.TrackedSessions, period.AttendedSessions, plan.MentorReviewPlan, plan.MentorReviewSessionAt, plan.MentorReviewCompletedAt,
            CanPlanReview = actor.Role == Role.Mentor && outcome is { Decision: "ReviewEligible" } && outcome.MentorId == actor.Id });
    }

    public async Task<ApiResponse<object>> RecordPracticeProgressAsync(int id, RecordMentorPracticeResultRequest request, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Learner) return ApiResponse<object>.Fail("Chỉ học viên được xác nhận lượt thi.", HttpStatusCode.Forbidden);
        var response = ApiResponse<object>.NotFound("Không tìm thấy lịch ôn luyện.");
        try
        {
            await unit.ExecuteInTransactionAsync(async token =>
            {
                await unit.UserAccounts.GetByIdForUpdateAsync(actor.Id, token);
                var booking = await ProgressBooking(actor, id).AsTracking().SingleOrDefaultAsync(token);
                if (booking is null) return;
                var plan = booking.UserSubscription;
                if (ReadPracticeOutcome(plan) is { } previous)
                {
                    response = previous.After.AttemptId == request.AttemptId ? ApiResponse<object>.Success(previous)
                        : ApiResponse<object>.Fail("Gói ôn luyện đã chốt kết quả cuối kỳ.", HttpStatusCode.Conflict);
                    return;
                }
                var period = await GetPeriodAsync(booking, token);
                if (period.WaitingReason is not null || period.LevelId is null || period.Start is null || period.End is null || period.LeadMentorId is null)
                {
                    response = ApiResponse<object>.Fail(period.WaitingReason ?? "Gói học chưa có cấp độ để đối chiếu thi thử.", HttpStatusCode.Conflict);
                    return;
                }
                var attempt = await ExamDetails(FinalExams(actor.Id, period.LevelId.Value, period.End.Value))
                    .SingleOrDefaultAsync(a => a.Id == request.AttemptId, token);
                var after = attempt is null ? null : ToExamResult(attempt, period.LevelId.Value);
                if (after is null || after.Sections.Select(s => JlptSkillAssessmentPolicy.Classify(s.LanguageSkillName)).Distinct().Count() != 4)
                {
                    response = ApiResponse<object>.Fail("Chọn lượt thi thử cùng cấp, đủ bốn kỹ năng, bắt đầu sau buổi ôn cuối.");
                    return;
                }
                var decision = after.Percent >= MockPassPercent ? "Passed" : period.AssignedCount == 0 ? "NoHomework"
                    : (long)period.SubmittedCount * 100 > (long)period.AssignedCount * 90 ? "ReviewEligible" : "HomeworkIncomplete";
                var outcome = new PracticeOutcome(await FindBaselineAsync(actor.Id, period.LevelId.Value, period.Start.Value, token),
                    after, decision, period.LeadMentorId.Value, period.AssignedCount, period.SubmittedCount, DateTime.UtcNow);
                plan.MentorPracticeOutcomeJson = JsonSerializer.Serialize(outcome, HomeworkJson);
                if (decision == "ReviewEligible")
                    await unit.Notifications.AddAsync(new Notification { UserId = outcome.MentorId, Title = "Học viên đủ điều kiện kèm lại",
                        Message = "Mở Bài tập về nhà để lên kế hoạch kèm lại.", Type = "Homework", LinkUrl = "/mentor-homework/" }, token);
                await unit.SaveChangesAsync(token);
                response = ApiResponse<object>.Success(outcome);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        { return ApiResponse<object>.Fail("Thông tin gói vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict); }
        return response;
    }

    public async Task<ApiResponse<object>> SaveReviewPlanAsync(int id, SaveMentorReviewPlanRequest request, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Mentor) return ApiResponse<object>.Fail("Chỉ mentor được lập kế hoạch kèm lại.", HttpStatusCode.Forbidden);
        var text = request.Plan?.Trim();
        if (text?.Length is not (> 0 and <= 6000)) return ApiResponse<object>.Fail("Nhập kế hoạch kèm lại tối đa 6000 ký tự.");
        var booking = await ProgressBooking(actor, id).AsTracking().SingleOrDefaultAsync(ct);
        if (booking is null) return ApiResponse<object>.NotFound("Không tìm thấy lịch ôn luyện.");
        var plan = booking.UserSubscription;
        if (ReadPracticeOutcome(plan) is not { Decision: "ReviewEligible" } outcome || outcome.MentorId != actor.Id)
            return ApiResponse<object>.Fail("Gói này chưa đủ điều kiện kèm lại hoặc bạn không phụ trách kèm lại.", HttpStatusCode.Forbidden);
        if (plan.MentorReviewCompletedAt != null) return ApiResponse<object>.Fail("Đã hoàn thành kèm lại.", HttpStatusCode.Conflict);
        if (request.Completed && (plan.MentorReviewPlan is null || plan.MentorReviewSessionAt > DateTime.UtcNow))
            return ApiResponse<object>.Fail("Hãy gửi và thực hiện kế hoạch trước khi đánh dấu hoàn thành.", HttpStatusCode.Conflict);
        if (request.Completed) plan.MentorReviewCompletedAt = DateTime.UtcNow;
        else
        {
            plan.MentorReviewPlan = text;
            plan.MentorReviewSessionAt = request.SessionAt?.UtcDateTime;
        }
        await NotifyAsync(plan.UserId, request.Completed ? "Đã hoàn thành kèm lại" : "Mentor đã gửi kế hoạch kèm lại", "Mở Bài tập về nhà để xem.", id, ct);
        try { await unit.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return ApiResponse<object>.Fail("Kế hoạch vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict); }
        return ApiResponse<object>.Success(new { plan.Id });
    }
}
