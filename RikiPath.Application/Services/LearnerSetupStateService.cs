using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Authoritative prerequisite resolver for flexible Learner setup:
/// Independent Goal (N5–N2) &amp; Placement Assessment (flexible order) ->
/// Placement Compatibility &amp; Explicit Skip -> Roadmap Eligibility (Standard / AI) ->
/// Initialized Roadmap State -> Scoped Progress.
/// </summary>
public sealed class LearnerSetupStateService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IMentorGroupPolicyService? groupPolicies = null) : ILearnerSetupStateService
{
    private const int DefaultPlacementAttemptLimit = 2;
    public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

    public async Task<ApiResponse<LearnerSetupStateResponse>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var state = await ResolveForUserAsync(userId, includeProgress: true, cancellationToken);
            if (state is null)
                return ApiResponse<LearnerSetupStateResponse>.NotFound("Không tìm thấy người dùng.");

            return ApiResponse<LearnerSetupStateResponse>.Success(state);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ApiResponse<LearnerSetupStateResponse>.Fail(
                "Không thể kiểm tra trạng thái thiết lập lộ trình học.",
                HttpStatusCode.InternalServerError);
        }
    }

    public async Task<bool> HasValidCompletedPlacementForTargetAsync(
        int userId,
        int targetLevelId,
        CancellationToken cancellationToken = default)
    {
        var targetLevel = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(targetLevelId, cancellationToken);
        var targetCode = targetLevel?.Code;
        var attempts = await LoadPlacementAttemptsAsync(userId, cancellationToken);
        var compatibility = PracticeTestService.EvaluatePlacementCompatibility(attempts, targetLevelId, targetCode);
        return compatibility.Status == "COMPATIBLE";
    }

    public async Task<LearnerSetupStateResponse?> ResolveForUserAsync(
        int userId,
        bool includeProgress = true,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted)
            return null;

        var utcNow = BusinessDateHelper.EnsureUtc(UtcNowProvider());

        // 1. Validate confirmed learning goal (N5–N2, active JLPT level, valid daily minutes)
        CertificateLevel? targetLevel = null;
        if (user.TargetCertificateLevelId is int targetId)
        {
            targetLevel = user.TargetCertificateLevel is { IsDeleted: false } loaded && loaded.Id == targetId
                ? loaded
                : await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(targetId, cancellationToken);
            if (targetLevel is not null && !JlptCurriculum.IsSupportedStudyLevel(targetLevel.Code))
                targetLevel = null;
        }

        CertificateLevel? startingLevel = null;
        if (user.StartingCertificateLevelId is int startId)
        {
            startingLevel = user.StartingCertificateLevel is { IsDeleted: false } loadedStart && loadedStart.Id == startId
                ? loadedStart
                : await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(startId, cancellationToken);
            if (startingLevel is not null && !JlptCurriculum.IsSupportedStudyLevel(startingLevel.Code))
                startingLevel = null;
        }

        CertificateLevel? assessedLevel = null;
        if (user.AssessedCertificateLevelId is int assessedId)
        {
            assessedLevel = user.AssessedCertificateLevel is { IsDeleted: false } loadedAssessed && loadedAssessed.Id == assessedId
                ? loadedAssessed
                : await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(assessedId, cancellationToken);
        }

        var hasValidMinutes = user.DailyStudyMinutes > 0 && user.DailyStudyMinutes <= 1440;
        var hasConfirmedGoal = targetLevel is not null && hasValidMinutes;
        var hasValidExamDate = user.TargetExamDate is not { } examDate ||
            (BusinessDateHelper.IsJlptExamDate(examDate) && BusinessDateHelper.IsFutureExamDate(examDate, utcNow));

        var response = new LearnerSetupStateResponse
        {
            OnboardingCompleted = hasConfirmedGoal,
            HasConfirmedGoal = hasConfirmedGoal,
            CertificateTypeId = targetLevel?.CertificateTypeId,
            CertificateTypeCode = targetLevel?.CertificateType?.Name ?? (targetLevel is not null ? "JLPT" : null),
            TargetJlptLevelId = hasConfirmedGoal ? targetLevel!.Id : null,
            TargetJlptLevelName = hasConfirmedGoal ? targetLevel!.Code : null,
            StartingJlptLevelId = startingLevel?.Id ?? (hasConfirmedGoal ? targetLevel!.Id : null),
            StartingJlptLevelName = startingLevel?.Code ?? (hasConfirmedGoal ? targetLevel!.Code : null),
            DailyStudyMinutes = user.DailyStudyMinutes,
            DailyStudyMinutesMin = user.DailyStudyMinutesMin,
            DailyStudyMinutesMax = user.DailyStudyMinutesMax,
            TargetExamDate = user.TargetExamDate,
            HasValidExamDate = hasValidExamDate,
            LearningGoalVersion = user.LearningGoalVersion,
        };

        // 2. Always evaluate placement attempts, compatibility & quota independently of Goal
        int? resolvedTargetId = hasConfirmedGoal ? targetLevel!.Id : null;
        string? resolvedTargetCode = hasConfirmedGoal ? targetLevel!.Code : null;

        var attempts = await LoadPlacementAttemptsAsync(userId, cancellationToken);
        var activeAttemptId = PracticeTestService.FindActivePlacementAttemptId(attempts, utcNow, resolvedTargetId, anyScope: true);
        var pendingSubmissionId = PracticeTestService.FindPendingSubmissionAttemptId(attempts, utcNow);
        var compatibility = PracticeTestService.EvaluatePlacementCompatibility(attempts, resolvedTargetId, resolvedTargetCode);

        // Preserve existing learners who already have a valid legacy (null-target) completed placement AND an active AI path
        var allPaths = await unitOfWork.RecommendedLearningPaths.GetByUserIdAsync(userId);
        var activeAiPath = allPaths
            .Where(p => !p.IsDeleted && p.Status == LearningPathStatus.Active)
            .OrderByDescending(p => p.Version)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();
        var draftAiPreview = allPaths
            .Where(p => !p.IsDeleted &&
                        p.Status == LearningPathStatus.Draft &&
                        (!p.PreviewExpiresAt.HasValue || BusinessDateHelper.EnsureUtc(p.PreviewExpiresAt.Value) > utcNow))
            .OrderByDescending(p => p.Version)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();

        var activeAiMatchesGoal = hasConfirmedGoal && activeAiPath is not null &&
            (!activeAiPath.TargetCertificateLevelId.HasValue ||
             activeAiPath.TargetCertificateLevelId.Value == resolvedTargetId);

        var latestCompleted = compatibility.CompatibleAttempt ?? compatibility.LatestAttempt;
        var recoveredCompleted = compatibility.CompatibleResult ?? compatibility.LatestResult;

        var isPlacementCompatible = compatibility.Status == "COMPATIBLE" || activeAiMatchesGoal;
        // Sequential flow: an assessment only counts once a goal is confirmed (Goal -> Placement -> Roadmap).
        var hasValidCompletedPlacement = hasConfirmedGoal && isPlacementCompatible;
        var isExplicitlySkipped = hasConfirmedGoal && !isPlacementCompatible && user.PlacementDecision == PlacementDecision.Skipped;

        // Skipping the assessment no longer opens a new roadmap. Learners who already study on a standard roadmap
        // opened by an earlier skip keep it (journey event or real lesson progress) — no data is removed.
        var standardPrefix = JourneyEventRecorder.Prefix(JourneyEventRecorder.StandardRoadmapReady, userId);
        var hasStandardReadyEvent = await unitOfWork.Notifications.Query()
            .AsNoTracking()
            .AnyAsync(n => n.UserId == userId &&
                           n.Type == JourneyEventRecorder.NotificationType &&
                           n.IdempotencyKey != null &&
                           n.IdempotencyKey.StartsWith(standardPrefix), cancellationToken);
        var progresses = await unitOfWork.LessonProgresses.GetByUserAsync(userId, cancellationToken);
        var hasLegacyLessonProgress = progresses.Any(p => !p.IsDeleted && (p.IsCompleted || p.ProgressPercent > 0));
        var keepsLegacySkippedRoadmap = isExplicitlySkipped && (hasStandardReadyEvent || hasLegacyLessonProgress);

        // Compute placement quota for current scope (target level if confirmed, else comprehensive null scope)
        var allUserAttempts = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
            .Where(a => a.UserId == userId && a.AttemptKind == AttemptKind.Placement)
            .Select(a => new { a.QuestionSnapshotJson })
            .ToListAsync(cancellationToken);
        var startedInScope = 0;
        foreach (var att in allUserAttempts)
        {
            if (PracticeTestService.TryDeserializeSnapshot(att.QuestionSnapshotJson, out var snap) &&
                !snap.ParentAssessmentAttemptId.HasValue &&
                snap.Questions is { Count: > 0 } &&
                snap.AssessmentTargetLevelId == resolvedTargetId)
            {
                startedInScope++;
            }
        }
        var policyLimit = groupPolicies is null
            ? null
            : (await groupPolicies.GetEffectiveAsync(utcNow, cancellationToken))?.PlacementAttemptLimit;
        var attemptLimit = policyLimit is > 0 ? policyLimit.Value : DefaultPlacementAttemptLimit;
        var attemptsRemaining = Math.Max(0, attemptLimit - startedInScope);

        response.PlacementQuotaInitialized = true;
        response.PlacementAttemptLimit = attemptLimit;
        response.PlacementAttemptsStarted = startedInScope;
        response.PlacementAttemptsRemaining = attemptsRemaining;
        if (activeAttemptId.HasValue)
        {
            response.PlacementCanStart = false;
            response.PlacementCannotStartReason = "ACTIVE_ATTEMPT_EXISTS";
        }
        else if (pendingSubmissionId.HasValue)
        {
            response.PlacementCanStart = false;
            response.PlacementCannotStartReason = "PENDING_SUBMISSION";
        }
        else if (attemptsRemaining <= 0)
        {
            response.PlacementCanStart = false;
            response.PlacementCannotStartReason = "PLACEMENT_ATTEMPT_LIMIT_REACHED";
        }
        else
        {
            response.PlacementCanStart = true;
            response.PlacementCannotStartReason = null;
        }

        response.HasActivePlacementAttempt = activeAttemptId.HasValue;
        response.ActivePlacementAttemptId = activeAttemptId;
        response.HasPendingPlacementSubmission = pendingSubmissionId.HasValue;
        response.PendingSubmissionAttemptId = pendingSubmissionId;
        response.HasValidCompletedPlacement = hasValidCompletedPlacement;
        response.LatestValidPlacementAttemptId = latestCompleted?.Id;
        response.LatestPlacementScorePercent = latestCompleted?.TotalScore;
        response.AssessedJlptLevelId = recoveredCompleted is { AssessedJlptLevelId: > 0 }
            ? recoveredCompleted.AssessedJlptLevelId
            : assessedLevel?.Id;
        response.AssessedJlptLevelName = !string.IsNullOrWhiteSpace(recoveredCompleted?.AssessedJlptLevelName)
            ? recoveredCompleted!.AssessedJlptLevelName
            : assessedLevel?.Code;
        response.LatestPlacementAssessmentLevelId = recoveredCompleted?.AssessmentTargetLevelId;
        response.LatestPlacementAssessmentLevelName = recoveredCompleted?.ScreeningLevelName
            ?? compatibility.CoveredLevelName;
        response.LatestPlacementIsComprehensive = recoveredCompleted?.IsComprehensiveAssessment ?? false;
        response.PlacementCompatibilityStatus = activeAiMatchesGoal && compatibility.Status != "COMPATIBLE"
            ? "COMPATIBLE"
            : compatibility.Status;
        response.PlacementCompatibilityReason = activeAiMatchesGoal && compatibility.Status != "COMPATIBLE"
            ? null
            : compatibility.Reason;
        response.IsPlacementExplicitlySkipped = isExplicitlySkipped;

        if (hasValidCompletedPlacement)
        {
            response.PlacementStatus = "COMPLETED";
            response.PlacementDecision = "COMPLETED";
        }
        else if (activeAttemptId.HasValue)
        {
            response.PlacementStatus = "IN_PROGRESS";
            response.PlacementDecision = isExplicitlySkipped ? "SKIPPED" : "NOT_TAKEN";
        }
        else if (pendingSubmissionId.HasValue)
        {
            response.PlacementStatus = "PENDING_SUBMISSION";
            response.PlacementDecision = isExplicitlySkipped ? "SKIPPED" : "NOT_TAKEN";
        }
        else if (isExplicitlySkipped)
        {
            response.PlacementStatus = "EXPLICITLY_SKIPPED";
            response.PlacementDecision = "SKIPPED";
        }
        else
        {
            response.PlacementStatus = "NOT_STARTED";
            response.PlacementDecision = "NOT_TAKEN";
        }

        // 3. Separate Roadmap Eligibility, AI Generation State, Initialization & Active Selection
        response.StandardRoadmapEligible = hasConfirmedGoal && (isPlacementCompatible || keepsLegacySkippedRoadmap);
        response.AiRoadmapEligible = hasConfirmedGoal && isPlacementCompatible;

        response.HasActiveAiRoadmap = activeAiPath is not null;
        response.ActiveAiRoadmapMatchesGoal = activeAiMatchesGoal;
        response.ActiveAiRoadmapId = activeAiPath?.Id;
        response.ActiveAiRoadmapTargetLevelId = activeAiPath?.TargetCertificateLevelId;
        response.HasDraftAiRoadmapPreview = draftAiPreview is not null;
        response.DraftAiRoadmapPreviewId = draftAiPreview?.Id;

        if (!response.AiRoadmapEligible)
        {
            response.AiRoadmapState = "NOT_ELIGIBLE";
        }
        else if (activeAiMatchesGoal)
        {
            response.AiRoadmapState = "READY";
        }
        else if (draftAiPreview is not null &&
                 (!draftAiPreview.TargetCertificateLevelId.HasValue ||
                  draftAiPreview.TargetCertificateLevelId.Value == resolvedTargetId))
        {
            response.AiRoadmapState = "DRAFT_PREVIEW";
        }
        else
        {
            response.AiRoadmapState = "NOT_GENERATED";
        }

        if (!hasConfirmedGoal)
        {
            response.HasInitializedRoadmap = false;
            response.ActiveRoadmapType = "NONE";
            response.HasAnyValidRoadmap = false;
            response.SetupStage = "GOAL_REQUIRED";
            response.NextAction = "COMPLETE_ONBOARDING";
            response.BlockingErrorCode = "STUDY_LEVEL_REQUIRED";
            response.HasValidRoadmapProgress = false;
            return response;
        }

        if (!response.StandardRoadmapEligible)
        {
            response.HasInitializedRoadmap = false;
            response.ActiveRoadmapType = "NONE";
            response.HasAnyValidRoadmap = false;
            if (activeAttemptId.HasValue)
            {
                response.SetupStage = "PLACEMENT_IN_PROGRESS";
                response.NextAction = "RESUME_PLACEMENT";
                response.BlockingErrorCode = "PLACEMENT_IN_PROGRESS";
            }
            else if (pendingSubmissionId.HasValue)
            {
                response.SetupStage = "PLACEMENT_IN_PROGRESS";
                response.NextAction = "SUBMIT_EXPIRED_PLACEMENT";
                response.BlockingErrorCode = "PLACEMENT_PENDING_SUBMISSION";
            }
            else if (compatibility.Status == "INSUFFICIENT_FOR_TARGET")
            {
                response.SetupStage = "PLACEMENT_REQUIRED";
                response.NextAction = "SUPPLEMENT_PLACEMENT";
                response.BlockingErrorCode = "PLACEMENT_INSUFFICIENT_FOR_TARGET";
            }
            else
            {
                response.SetupStage = "PLACEMENT_REQUIRED";
                response.NextAction = "START_PLACEMENT";
                response.BlockingErrorCode = "STANDARD_ROADMAP_PLACEMENT_REQUIRED";
            }

            response.HasValidRoadmapProgress = false;
            return response;
        }

        // Standard Roadmap (and optionally AI) is eligible. Check whether any roadmap has been initialized yet.
        var hasInitializedRoadmap = activeAiMatchesGoal
            || keepsLegacySkippedRoadmap
            || hasStandardReadyEvent
            || (hasLegacyLessonProgress && response.StandardRoadmapEligible);

        response.HasInitializedRoadmap = hasInitializedRoadmap;
        response.HasAnyValidRoadmap = hasInitializedRoadmap;
        response.BlockingErrorCode = null;

        if (!hasInitializedRoadmap)
        {
            response.ActiveRoadmapType = "NONE";
            response.SetupStage = response.AiRoadmapEligible ? "AI_READY_TO_GENERATE" : "STANDARD_READY";
            response.NextAction = response.AiRoadmapEligible ? "GENERATE_AI_ROADMAP" : "OPEN_STANDARD_ROADMAP";
            response.HasValidRoadmapProgress = false;
            return response;
        }

        if (response.AiRoadmapState == "READY" && user.LearningMode == LearningMode.AiPersonalized)
        {
            response.ActiveRoadmapType = "AI_PERSONALIZED";
            response.SetupStage = "ROADMAP_ACTIVE";
            response.NextAction = "CONTINUE_ROADMAP";
        }
        else if (response.AiRoadmapState == "READY")
        {
            response.ActiveRoadmapType = "STANDARD";
            response.SetupStage = "ROADMAP_ACTIVE";
            response.NextAction = "CONTINUE_ROADMAP";
        }
        else
        {
            response.ActiveRoadmapType = "STANDARD";
            response.SetupStage = "ROADMAP_ACTIVE";
            response.NextAction = "CONTINUE_ROADMAP";
        }

        // 4. Scoped lesson progress strictly when a roadmap is initialized
        if (includeProgress && resolvedTargetId.HasValue && targetLevel is not null)
        {
            var scopedLevelIds = new HashSet<int> { resolvedTargetId.Value };
            if (startingLevel is not null &&
                startingLevel.Id != resolvedTargetId.Value &&
                JlptCurriculum.Rank(startingLevel.Code) < JlptCurriculum.Rank(targetLevel.Code))
            {
                scopedLevelIds.Add(startingLevel.Id);
            }

            var scopedLessonIds = new HashSet<int>();
            foreach (var lvlId in scopedLevelIds)
            {
                var levelLessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(lvlId, cancellationToken);
                foreach (var lesson in levelLessons)
                    scopedLessonIds.Add(lesson.Id);
            }

            var completedCount = progresses
                .Where(p => scopedLessonIds.Contains(p.LessonId) && (p.IsCompleted || p.ProgressPercent >= 100))
                .Select(p => p.LessonId)
                .Distinct()
                .Count();

            var totalCount = scopedLessonIds.Count;
            response.HasValidRoadmapProgress = totalCount > 0;
            response.RoadmapTotalLessons = totalCount;
            response.RoadmapCompletedLessons = completedCount;
            response.RoadmapCompletionPercent = totalCount > 0
                ? Math.Round(completedCount * 100d / totalCount, 2)
                : 0d;
        }

        return response;
    }

    private async Task<List<MockTestAttempt>> LoadPlacementAttemptsAsync(int userId, CancellationToken cancellationToken)
    {
        var attempts = await unitOfWork.MockTestAttempts.GetPlacementByUserAsync(userId, cancellationToken);
        if (attempts is { Count: > 0 })
        {
            return attempts
                .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.Placement && a.UserId == userId)
                .OrderByDescending(a => a.StartedAt)
                .ThenByDescending(a => a.Id)
                .ToList();
        }

        var fallback = await unitOfWork.MockTestAttempts.FindAsync(
            a => a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.Placement,
            cancellationToken);
        return (fallback ?? [])
            .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.Placement && a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ThenByDescending(a => a.Id)
            .ToList();
    }
}
