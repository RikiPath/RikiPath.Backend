using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorLearnerSkillProfileService(
    IUnitOfWork uow,
    IClaimService claims) : IMentorLearnerSkillProfileService
{
    private static readonly string[] CanonicalSkills =
        ["Vocabulary", "Kanji", "Grammar", "Listening", "Reading", "Writing", "Speaking"];

    public async Task<ApiResponse<MentorLearnerSkillProfileResponse>> GetSkillProfileAsync(
        int learnerId, int? cohortId = null, CancellationToken cancellationToken = default)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Mentor)
            return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                "Chỉ cố vấn mới được xem hồ sơ kỹ năng học viên.", HttpStatusCode.Forbidden);
        if (learnerId <= 0)
            return ApiResponse<MentorLearnerSkillProfileResponse>.Fail("Học viên không hợp lệ.");

        int? enrollmentId = null;
        string scopeType;
        if (cohortId.HasValue)
        {
            var cohortEnrollment = await uow.StudyCourses.Query().AsNoTracking()
                .Where(c => c.CohortId == cohortId.Value
                    && c.LearnerId == learnerId
                    && c.PaymentStatus == "Paid"
                    && (c.Status == "Active" || c.Status == "Completed" || c.Status == "Ready")
                    && c.Cohort != null && c.Cohort.MentorId == actor.Id)
                .OrderByDescending(c => c.Id)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (cohortEnrollment is null)
                return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                    "Bạn chỉ được xem hồ sơ học viên thuộc lớp nhóm do mình phụ trách.", HttpStatusCode.Forbidden);
            enrollmentId = cohortEnrollment.Id;
            scopeType = "Cohort";
        }
        else
        {
            var courseEnrollment = await uow.StudyCourses.Query().AsNoTracking()
                .Where(c => c.LearnerId == learnerId
                    && c.PaymentStatus == "Paid"
                    && (c.Status == "Active" || c.Status == "Completed" || c.Status == "Ready")
                    && (c.MentorId == actor.Id || (c.Cohort != null && c.Cohort.MentorId == actor.Id)))
                .OrderByDescending(c => c.Id)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (courseEnrollment is null)
                return ApiResponse<MentorLearnerSkillProfileResponse>.Fail(
                    "Bạn không phụ trách học viên này.", HttpStatusCode.Forbidden);
            enrollmentId = courseEnrollment.Id;
            scopeType = "Course";
        }

        var learner = await uow.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == learnerId && !u.IsDeleted)
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .SingleOrDefaultAsync(cancellationToken);
        if (learner is null)
            return ApiResponse<MentorLearnerSkillProfileResponse>.NotFound("Không tìm thấy học viên.");

        var samples = new Dictionary<string, List<(double Score, DateTime At, string Source)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in CanonicalSkills)
            samples[skill] = [];

        var mockSectionScores = await uow.MockTestSectionResults.Query().AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.TotalCount > 0
                && !r.MockTestAttempt.IsDeleted
                && r.MockTestAttempt.UserId == learnerId
                && r.MockTestAttempt.IsCompleted)
            .Select(r => new
            {
                Skill = r.MockTestSection.LanguageSkill.Name,
                r.ScorePercent,
                At = r.MockTestAttempt.SubmittedAt ?? r.MockTestAttempt.StartedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var item in mockSectionScores)
            if (samples.TryGetValue(item.Skill, out var list))
                list.Add((item.ScorePercent, item.At, "MockTest"));

        var practiceScores = await uow.PracticeExerciseAttempts.Query().AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.UserId == learnerId
                && a.IsCompleted
                && a.ScorePercent.HasValue)
            .Select(a => new
            {
                Skill = a.PracticeExercise.LanguageSkill.Name,
                Score = a.ScorePercent!.Value,
                At = a.SubmittedAt ?? a.StartedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var item in practiceScores)
            if (samples.TryGetValue(item.Skill, out var list))
                list.Add((item.Score, item.At, "PracticeExercise"));

        var submissionGrades = await uow.GradingResults.Query().AsNoTracking()
            .Where(g => !g.IsDeleted
                && g.PracticeSubmission != null
                && !g.PracticeSubmission.IsDeleted
                && g.PracticeSubmission.UserId == learnerId
                && (g.PracticeSubmission.Type == SubmissionType.Writing || g.PracticeSubmission.Type == SubmissionType.Speaking))
            .Select(g => new
            {
                Type = g.PracticeSubmission!.Type,
                g.OverallScore,
                At = g.GradedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var g in submissionGrades)
        {
            var key = g.Type == SubmissionType.Speaking ? "Speaking" : "Writing";
            samples[key].Add((Math.Clamp(g.OverallScore, 0, 100), g.At, "AiPracticeGrading"));
        }

        var latestPlacement = await uow.MockTestAttempts.Query().AsNoTracking()
            .Where(a => !a.IsDeleted && a.UserId == learnerId && a.AttemptKind == AttemptKind.Placement && a.IsCompleted)
            .OrderByDescending(a => a.SubmittedAt ?? a.StartedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.QuestionSnapshotJson)
            .FirstOrDefaultAsync(cancellationToken);
        var placementResult = latestPlacement is null ? null : PracticeTestService.TryGetCompletedResultFromSnapshot(latestPlacement);

        var skillMetrics = CanonicalSkills.Select(skill =>
        {
            var list = samples[skill];
            if (list.Count == 0)
                return new LearnerSkillMetricResponse(skill, null, 0, null, null);
            var avg = Math.Round(list.Average(x => x.Score), 2);
            var latestAt = list.Max(x => x.At);
            var source = string.Join(",", list.Select(x => x.Source).Distinct().OrderBy(x => x));
            return new LearnerSkillMetricResponse(skill, avg, list.Count, latestAt, source);
        }).ToList();

        // Scoped homework stats (cohort-isolated when cohortId is supplied; mentor-isolated otherwise).
        var courseHwQuery = uow.StudyCourses.Query().AsNoTracking()
            .Where(c => c.LearnerId == learnerId && c.PaymentStatus == "Paid" && (c.Status == "Active" || c.Status == "Completed"));
        if (cohortId.HasValue)
            courseHwQuery = courseHwQuery.Where(c => c.CohortId == cohortId.Value);
        else
            courseHwQuery = courseHwQuery.Where(c => c.MentorId == actor.Id || (c.Cohort != null && c.Cohort.MentorId == actor.Id));

        var courseHw = await courseHwQuery.SelectMany(c => c.Homework)
            .Select(h => new { h.DueAt, h.SubmittedAt, h.Passed, h.ReviewedAt })
            .ToListAsync(cancellationToken);

        var cohortAssignmentsQuery = uow.HomeworkAssignments.Query().AsNoTracking()
            .Where(h => !h.IsDeleted && h.LearnerId == learnerId && h.MentorId == actor.Id);
        cohortAssignmentsQuery = cohortId.HasValue
            ? cohortAssignmentsQuery.Where(h => h.CohortId == cohortId.Value)
            : cohortAssignmentsQuery.Where(h => !h.CohortId.HasValue);
        var hwAssignments = await cohortAssignmentsQuery
            .Select(h => new
            {
                h.DueAt,
                SubmittedAt = h.Submission != null && !h.Submission.IsDeleted ? (DateTime?)h.Submission.SubmittedAt : null,
                h.Passed,
                h.PublishedFeedbackAtUtc
            })
            .ToListAsync(cancellationToken);

        var assignedCount = courseHw.Count + hwAssignments.Count;
        var submittedCount = courseHw.Count(h => h.SubmittedAt.HasValue) + hwAssignments.Count(h => h.SubmittedAt.HasValue);
        var onTimeCount = courseHw.Count(h => h.SubmittedAt.HasValue && h.SubmittedAt.Value <= h.DueAt)
            + hwAssignments.Count(h => h.SubmittedAt.HasValue && (!h.DueAt.HasValue || h.SubmittedAt.Value <= h.DueAt.Value));
        var passedCount = courseHw.Count(h => h.Passed == true) + hwAssignments.Count(h => h.Passed == true);
        var activityDates = courseHw
            .SelectMany(h => new DateTime?[] { h.SubmittedAt, h.ReviewedAt })
            .Concat(hwAssignments.SelectMany(h => new DateTime?[] { h.SubmittedAt, h.PublishedFeedbackAtUtc }))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();
        DateTime? latestHomeworkAt = activityDates.Count > 0 ? activityDates.Max() : null;
        double? onTimePercent = assignedCount > 0 ? Math.Round(onTimeCount * 100d / assignedCount, 2) : null;

        var hwStats = new LearnerCohortHomeworkStatsResponse(
            assignedCount, submittedCount, onTimeCount, passedCount, onTimePercent, latestHomeworkAt);

        return ApiResponse<MentorLearnerSkillProfileResponse>.Success(new MentorLearnerSkillProfileResponse(
            learner.Id,
            $"{learner.FirstName} {learner.LastName}".Trim(),
            cohortId,
            scopeType,
            enrollmentId,
            placementResult?.AssessedJlptLevelName,
            skillMetrics,
            hwStats));
    }
}
