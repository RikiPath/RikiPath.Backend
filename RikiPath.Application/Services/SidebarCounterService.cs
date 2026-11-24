using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Sidebar;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Centralized, role-aware sidebar action/due counter service backed by live DB aggregations.
/// Every EF query executes sequentially on the scoped UnitOfWork DbContext and enforces strict RBAC
/// and ownership filtering from <see cref="IClaimService"/>.
/// </summary>
public sealed class SidebarCounterService(
    IUnitOfWork unitOfWork,
    IClaimService claimService) : ISidebarCounterService
{
    public async Task<ApiResponse<SidebarCountersResponse>> GetMyCountersAsync(
        CancellationToken cancellationToken = default)
    {
        var actor = claimService.GetUserClaim();
        if (actor.Id <= 0)
            return ApiResponse<SidebarCountersResponse>.Fail("Phiên đăng nhập không hợp lệ.", HttpStatusCode.Unauthorized);

        var userExists = await unitOfWork.UserAccounts.Query()
            .AsNoTracking()
            .AnyAsync(u => u.Id == actor.Id && !u.IsDeleted && u.Role == actor.Role, cancellationToken);
        if (!userExists)
            return ApiResponse<SidebarCountersResponse>.Fail("Tài khoản không còn hiệu lực hoặc vai trò đã thay đổi.", HttpStatusCode.Unauthorized);

        var utcNow = DateTime.UtcNow;
        var response = new SidebarCountersResponse
        {
            Role = actor.Role.ToString(),
            UpdatedAtUtc = utcNow,
            ServerVersionMs = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds(),
            Counters = [],
        };

        // Execute queries sequentially: IUnitOfWork repositories share one scoped DbContext.
        switch (actor.Role)
        {
            case Role.Learner:
                await PopulateLearnerCountersAsync(actor.Id, utcNow, response, cancellationToken);
                break;

            case Role.Mentor:
                await PopulateMentorCountersAsync(actor.Id, utcNow, response, cancellationToken);
                break;

            case Role.Admin:
                await PopulateAdminCountersAsync(utcNow, response, cancellationToken);
                break;

            case Role.ContentAuthor:
                await PopulateContentAuthorCountersAsync(actor.Id, response, cancellationToken);
                break;

            default:
                return ApiResponse<SidebarCountersResponse>.Fail("Vai trò không được hỗ trợ.", HttpStatusCode.Forbidden);
        }

        return ApiResponse<SidebarCountersResponse>.Success(response);
    }

    private async Task PopulateLearnerCountersAsync(
        int userId,
        DateTime utcNow,
        SidebarCountersResponse response,
        CancellationToken ct)
    {
        var pref = await unitOfWork.UserSrsPreferences.Query()
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => new { p.DailyNewLimit, p.TimezoneId })
            .FirstOrDefaultAsync(ct);

        var dailyNewLimit = pref?.DailyNewLimit ?? 20;
        var timezoneId = string.IsNullOrWhiteSpace(pref?.TimezoneId) ? "Asia/Ho_Chi_Minh" : pref!.TimezoneId;
        var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, timezoneId);
        var historyWindowStartUtc = studyToday.AddDays(-1);

        var recentNewReviewTimes = await unitOfWork.ReviewHistories.Query()
            .AsNoTracking()
            .Where(h => h.ReviewCard.UserId == userId
                && !h.IsDeleted
                && h.PreviousState == SrsLearningState.New
                && h.ReviewedAt >= historyWindowStartUtc)
            .Select(h => h.ReviewedAt)
            .ToListAsync(ct);

        var reviewedNewToday = recentNewReviewTimes
            .Count(reviewedAt => SrsSchedulingHelper.GetStudyDateInZone(reviewedAt, timezoneId) == studyToday);

        var dueGroups = await unitOfWork.ReviewCards.Query()
            .AsNoTracking()
            .Where(c => c.UserId == userId
                && !c.IsDeleted
                && c.Status == SrsCardStatus.Active
                && c.Mode == ReviewMode.Recognition
                && c.NextReviewDate <= utcNow)
            .GroupBy(c => c.LearningState == SrsLearningState.New)
            .Select(g => new { IsNew = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dueNewCount = dueGroups.Where(g => g.IsNew).Sum(g => g.Count);
        var dueNonNewCount = dueGroups.Where(g => !g.IsNew).Sum(g => g.Count);
        var actionableDueCount = SrsSchedulingHelper.ComputeActionableDueCount(
            dueNonNewCount,
            dueNewCount,
            dailyNewLimit,
            reviewedNewToday);

        var nextFutureDueRaw = await unitOfWork.ReviewCards.Query()
            .AsNoTracking()
            .Where(c => c.UserId == userId
                && !c.IsDeleted
                && c.Status == SrsCardStatus.Active
                && c.Mode == ReviewMode.Recognition
                && c.NextReviewDate > utcNow)
            .OrderBy(c => c.NextReviewDate)
            .Select(c => (DateTime?)c.NextReviewDate)
            .FirstOrDefaultAsync(ct);

        DateTime? nextDueUtc = nextFutureDueRaw.HasValue
            ? DateTime.SpecifyKind(nextFutureDueRaw.Value, DateTimeKind.Utc)
            : null;

        var remainingNewBudget = Math.Max(0, dailyNewLimit - reviewedNewToday);
        if (dueNewCount > remainingNewBudget)
        {
            var nextDayResetUtc = SrsSchedulingHelper.GetNextStudyDayStartUtc(utcNow, timezoneId);
            nextDueUtc = nextDueUtc.HasValue && nextDueUtc.Value < nextDayResetUtc
                ? nextDueUtc
                : nextDayResetUtc;
        }

        response.NextDueAtUtc = nextDueUtc;

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "learner.srs_due",
            Count = Math.Max(0, actionableDueCount),
            Kind = SidebarCounterKind.DueItems,
            Metadata = new Dictionary<string, object?>
            {
                ["dueNonNew"] = dueNonNewCount,
                ["dueNew"] = dueNewCount,
                ["dailyNewLimit"] = dailyNewLimit,
                ["reviewedNewToday"] = reviewedNewToday,
                ["nextDueAtUtc"] = nextDueUtc,
            },
        });

        var pendingHomework = await unitOfWork.HomeworkAssignments.Query()
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted
                && item.LearnerId == userId
                && item.Status == "Assigned"
                && (item.Submission == null || item.Submission.IsDeleted)
                && (item.StudyCourseId == null || unitOfWork.StudyCourses.Query().Any(c =>
                    c.Id == item.StudyCourseId
                    && c.Status != "Cancelled"
                    && c.Status != "PendingPayment")),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "learner.homework_pending",
            Count = Math.Max(0, pendingHomework),
            Kind = SidebarCounterKind.PendingUserAction,
        });
    }

    private async Task PopulateMentorCountersAsync(
        int mentorId,
        DateTime utcNow,
        SidebarCountersResponse response,
        CancellationToken ct)
    {
        var pendingAttendanceSessions = await unitOfWork.StudyCohorts.Query()
            .AsNoTracking()
            .Where(c => c.MentorId == mentorId && c.Status != "Cancelled" && c.Status != "Draft")
            .SelectMany(c => c.Sessions)
            .Where(s => !s.Availability.IsDeleted
                && s.Availability.EndTime <= utcNow
                && unitOfWork.StudyCourses.Query().Any(e =>
                    e.CohortId == s.CohortId
                    && e.PaymentStatus == "Paid"
                    && e.Status == "Active"
                    && e.Sessions.Any(cs => cs.AvailabilityId == s.AvailabilityId && cs.Status == "Scheduled")))
            .CountAsync(ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "mentor.cohort_attendance_pending",
            Count = Math.Max(0, pendingAttendanceSessions),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var homeworkToGrade = await unitOfWork.HomeworkAssignments.Query()
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted
                && (item.MentorId == mentorId
                    || (item.CohortId != null && item.StudyCourseId != null
                        && unitOfWork.StudyCourses.Query().Any(c =>
                            c.Id == item.StudyCourseId
                            && c.CohortId == item.CohortId
                            && c.Status == "Active"
                            && c.Cohort != null
                            && c.Cohort.MentorId == mentorId)))
                && item.Status == "Submitted"
                && item.PublishedFeedbackAtUtc == null
                && item.Submission != null
                && !item.Submission.IsDeleted
                && (item.Submission.GradingResult == null || item.Submission.GradingResult.IsDeleted),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "mentor.homework_to_grade",
            Count = Math.Max(0, homeworkToGrade),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var proposalsActionRequired = await unitOfWork.MentorTeachingProposals.Query()
            .AsNoTracking()
            .CountAsync(p => p.MentorId == mentorId
                && p.Status == MentorTeachingProposalStatus.ChangesRequested,
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "mentor.proposals_action_required",
            Count = Math.Max(0, proposalsActionRequired),
            Kind = SidebarCounterKind.PendingUserAction,
        });

        var complaintsAwaitingResponse = await unitOfWork.CohortComplaints.Query()
            .AsNoTracking()
            .CountAsync(c => c.MentorId == mentorId
                && c.Category != CohortComplaint.CategoryPaymentEnrollment
                && (c.Status == CohortComplaint.Open || c.Status == CohortComplaint.UnderReview)
                && c.MentorRespondedAtUtc == null,
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "mentor.complaints_awaiting_response",
            Count = Math.Max(0, complaintsAwaitingResponse),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var profileNeedsAction = await unitOfWork.MentorProfiles.Query()
            .AsNoTracking()
            .AnyAsync(p => !p.IsDeleted
                && p.MentorId == mentorId
                && (p.VerificationStatus == MentorVerificationStatus.ChangesRequested
                    || p.VerificationStatus == MentorVerificationStatus.Rejected),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "mentor.profile_action_required",
            Count = profileNeedsAction ? 1 : 0,
            Kind = SidebarCounterKind.PendingUserAction,
        });
    }

    private async Task PopulateAdminCountersAsync(
        DateTime utcNow,
        SidebarCountersResponse response,
        CancellationToken ct)
    {
        var paymentReviewCourses = await unitOfWork.StudyCourses.Query()
            .AsNoTracking()
            .CountAsync(x => x.Status == "PaymentReview", ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.payments_review",
            Count = Math.Max(0, paymentReviewCourses),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var retakeRequests = await unitOfWork.StudyCourses.Query()
            .AsNoTracking()
            .SelectMany(c => c.RetakeRequests)
            .CountAsync(r => r.Status == "Pending", ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.retake_requests",
            Count = Math.Max(0, retakeRequests),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var pendingProposals = await unitOfWork.MentorTeachingProposals.Query()
            .AsNoTracking()
            .CountAsync(p => p.Status == MentorTeachingProposalStatus.PendingReview, ct);

        // Exclude slots owned by a proposal so 1 proposal with multiple sessions is never counted multiple times.
        var pendingStandaloneSlots = await unitOfWork.MentorAvailabilities.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted
                && x.TeachingProposalId == null
                && !x.IsApproved
                && x.RejectedAt == null
                && x.StartTime > utcNow,
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.mentor_schedules",
            Count = Math.Max(0, pendingProposals) + Math.Max(0, pendingStandaloneSlots),
            Kind = SidebarCounterKind.ActionRequired,
            Metadata = new Dictionary<string, object?>
            {
                ["pendingProposals"] = pendingProposals,
                ["pendingStandaloneSlots"] = pendingStandaloneSlots,
            },
        });

        var pendingCohortScheduleRequests = await unitOfWork.CohortScheduleRequests.Query()
            .AsNoTracking()
            .CountAsync(x => x.Status == CohortScheduleRequest.Pending, ct);

        var pendingSlotCancelRequests = await unitOfWork.MentorAvailabilities.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted
                && x.CancelRequestedAt != null
                && x.CancelRejectedAt == null
                && x.StartTime > utcNow,
                ct);

        var pendingSlotChangeRequests = await unitOfWork.MentorAvailabilities.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted
                && x.ChangeRequestedAt != null
                && x.ChangeResolution == null
                && x.StartTime > utcNow,
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.schedule_requests",
            Count = Math.Max(0, pendingCohortScheduleRequests)
                + Math.Max(0, pendingSlotCancelRequests)
                + Math.Max(0, pendingSlotChangeRequests),
            Kind = SidebarCounterKind.ActionRequired,
            Metadata = new Dictionary<string, object?>
            {
                ["cohortScheduleRequests"] = pendingCohortScheduleRequests,
                ["slotCancelRequests"] = pendingSlotCancelRequests,
                ["slotChangeRequests"] = pendingSlotChangeRequests,
            },
        });

        var pendingTransfers = await unitOfWork.CohortTransferRequests.Query()
            .AsNoTracking()
            .CountAsync(x => x.Status == CohortTransferRequest.Pending, ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.cohort_transfers",
            Count = Math.Max(0, pendingTransfers),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var decidableComplaints = await unitOfWork.CohortComplaints.Query()
            .AsNoTracking()
            .CountAsync(x => x.Status == CohortComplaint.Open || x.Status == CohortComplaint.UnderReview, ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.complaints",
            Count = Math.Max(0, decidableComplaints),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var pendingVerifications = await unitOfWork.MentorProfiles.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted
                && x.VerificationStatus == MentorVerificationStatus.Pending
                && !x.Mentor.IsDeleted,
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.mentor_verifications",
            Count = Math.Max(0, pendingVerifications),
            Kind = SidebarCounterKind.ActionRequired,
        });

        var pendingApprovalRequests = await unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == ContentApprovalStatus.PendingReview, ct);

        var pendingSrsTemplates = await unitOfWork.SrsCardTemplates.Query()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == ContentStatus.PendingReview, ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "admin.content_review",
            Count = Math.Max(0, pendingApprovalRequests) + Math.Max(0, pendingSrsTemplates),
            Kind = SidebarCounterKind.ActionRequired,
            Metadata = new Dictionary<string, object?>
            {
                ["approvalRequests"] = pendingApprovalRequests,
                ["srsTemplates"] = pendingSrsTemplates,
            },
        });
    }

    private async Task PopulateContentAuthorCountersAsync(
        int authorId,
        SidebarCountersResponse response,
        CancellationToken ct)
    {
        var manualChanges = await unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted
                && r.SubmittedByUserId == authorId
                && (r.Status == ContentApprovalStatus.Rejected || r.Status == ContentApprovalStatus.ChangesRequested)
                && (r.EntityType == ImportEntityType.Kanji
                    || r.EntityType == ImportEntityType.Vocabulary
                    || r.EntityType == ImportEntityType.Grammar
                    || r.EntityType == ImportEntityType.PracticeExercise),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "author.manual_changes_required",
            Count = Math.Max(0, manualChanges),
            Kind = SidebarCounterKind.PendingUserAction,
        });

        var srsTemplateChanges = await unitOfWork.SrsCardTemplates.Query()
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted
                && t.SubmittedByUserId == authorId
                && (t.Status == ContentStatus.ChangesRequested || t.Status == ContentStatus.Rejected),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "author.srs_templates_changes_required",
            Count = Math.Max(0, srsTemplateChanges),
            Kind = SidebarCounterKind.PendingUserAction,
        });

        var lessonChanges = await unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted
                && r.SubmittedByUserId == authorId
                && r.EntityType == ImportEntityType.Lesson
                && (r.Status == ContentApprovalStatus.Rejected || r.Status == ContentApprovalStatus.ChangesRequested),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "author.lessons_changes_required",
            Count = Math.Max(0, lessonChanges),
            Kind = SidebarCounterKind.PendingUserAction,
        });

        var managedExamChanges = await unitOfWork.MockTests.Query()
            .AsNoTracking()
            .CountAsync(t => !t.IsDeleted
                && t.IsManagedWorkflow
                && t.AuthorUserId == authorId
                && (t.PublishStatus == ContentStatus.ChangesRequested || t.PublishStatus == ContentStatus.Rejected),
                ct);

        var legacyExamChanges = await unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted
                && r.SubmittedByUserId == authorId
                && r.EntityType == ImportEntityType.MockTest
                && r.TargetEntityId == null
                && (r.Status == ContentApprovalStatus.Rejected || r.Status == ContentApprovalStatus.ChangesRequested),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "author.exams_changes_required",
            Count = Math.Max(0, managedExamChanges) + Math.Max(0, legacyExamChanges),
            Kind = SidebarCounterKind.PendingUserAction,
        });

        var standaloneQuestionChanges = await unitOfWork.QuestionBankItems.Query()
            .AsNoTracking()
            .CountAsync(q => !q.IsDeleted
                && q.PassageId == null
                && q.AuthorUserId == authorId
                && (q.PublishStatus == ContentStatus.ChangesRequested || q.PublishStatus == ContentStatus.Rejected),
                ct);

        var passageChanges = await unitOfWork.ContentPassages.Query()
            .AsNoTracking()
            .CountAsync(p => !p.IsDeleted
                && p.AuthorUserId == authorId
                && (p.PublishStatus == ContentStatus.ChangesRequested || p.PublishStatus == ContentStatus.Rejected),
                ct);

        response.Counters.Add(new SidebarCounterItemDto
        {
            Key = "author.question_bank_changes_required",
            Count = Math.Max(0, standaloneQuestionChanges) + Math.Max(0, passageChanges),
            Kind = SidebarCounterKind.PendingUserAction,
        });
    }
}
