using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningMoments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningMoments;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    /// <summary>
    /// Learning Moments V1. Every moment is derived from data the backend already verified (saved learning goal,
    /// submitted + graded placement/mock attempts, completed lesson progress, confirmed learning path, finished
    /// scheduled SRS session with nothing left due). Opening a page never creates one.
    ///
    /// Presentation is exactly-once per learner + occurrence key: the claim inserts a <see cref="Notification"/>
    /// whose unique IdempotencyKey makes concurrent tabs/devices lose the race (no schema change needed).
    /// Only recent occurrences are offered as pop-ups so existing learners are not flooded with old milestones;
    /// everything earned stays visible in the achievement history.
    /// </summary>
    public class LearningMomentService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        IReviewService reviewService,
        IAnalyticsService analyticsService,
        IStandardRoadmapService standardRoadmapService) : ILearningMomentService
    {
        private const string MomentNotificationType = "LearningMoment";
        private static readonly TimeSpan RecentWindow = TimeSpan.FromDays(3);
        private static readonly TimeSpan NewAccountWindow = TimeSpan.FromDays(7);
        private static readonly int[] LessonCountBadges = [1, 5, 10, 25, 50, 100];

        private sealed record MomentCandidate(LearningMomentResponse Moment);

        private static string Key(string kind, int userId, string? suffix = null) =>
            suffix is null ? $"moment:{kind}:{userId}" : $"moment:{kind}:{userId}:{suffix}";

        private async Task<List<MomentCandidate>> BuildCandidatesAsync(int userId, DateTime utcNow, CancellationToken ct)
        {
            var candidates = new List<MomentCandidate>();
            var user = await unitOfWork.UserAccounts.Query()
                .AsNoTracking()
                .Include(u => u.TargetCertificateLevel)
                .Include(u => u.AssessedCertificateLevel)
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return candidates;

            var activePath = await unitOfWork.RecommendedLearningPaths.Query()
                .AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted && p.Status == LearningPathStatus.Active)
                .OrderByDescending(p => p.ConfirmedAt ?? p.GeneratedAt)
                .FirstOrDefaultAsync(ct);

            var hasConfirmedGoal = user.TargetCertificateLevelId.HasValue
                && user.DailyStudyMinutes is > 0 and <= 1440
                && user.TargetCertificateLevel is not null;

            var placementAttempts = await unitOfWork.MockTestAttempts.Query()
                .AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsDeleted
                    && (a.AttemptKind == AttemptKind.Placement || a.IsPlacementAssessment)
                    && a.IsCompleted && a.SubmittedAt != null && a.TotalScore != null)
                .OrderByDescending(a => a.SubmittedAt)
                .ThenByDescending(a => a.Id)
                .ToListAsync(ct);

            var compatibility = PracticeTestService.EvaluatePlacementCompatibility(
                placementAttempts,
                hasConfirmedGoal ? user.TargetCertificateLevelId : null,
                hasConfirmedGoal ? user.TargetCertificateLevel?.Code : null);

            var standardPrefix = JourneyEventRecorder.Prefix(JourneyEventRecorder.StandardRoadmapReady, userId);
            var standardEvent = await unitOfWork.Notifications.Query()
                .AsNoTracking()
                .Where(n => n.UserId == userId && n.Type == JourneyEventRecorder.NotificationType
                    && n.IdempotencyKey != null && n.IdempotencyKey.StartsWith(standardPrefix))
                .OrderByDescending(n => n.CreatedDate)
                .Select(n => new { n.IdempotencyKey, n.CreatedDate })
                .FirstOrDefaultAsync(ct);

            var roadmapAlreadyInitialized = activePath is not null
                || (hasConfirmedGoal && (user.PlacementDecision == PlacementDecision.Skipped || standardEvent is not null));

            // 1) Goal confirmed: emitted once per LearningGoalVersion.
            //    When user took placement BEFORE setting goal:
            //    - Compatible -> Case 3 ("Mọi thứ đã sẵn sàng cho hành trình JLPT!")
            //    - Insufficient -> compatibility guidance instead of premature "ready"
            //    - No placement yet -> Case 1 ("Chúc mừng! Mục tiêu JLPT của bạn đã được thiết lập")
            var goalPrefix = JourneyEventRecorder.Prefix(JourneyEventRecorder.GoalConfirmed, userId);
            var goalEvent = await unitOfWork.Notifications.Query()
                .AsNoTracking()
                .Where(n => n.UserId == userId && n.Type == JourneyEventRecorder.NotificationType
                    && n.IdempotencyKey != null && n.IdempotencyKey.StartsWith(goalPrefix))
                .OrderByDescending(n => n.CreatedDate)
                .Select(n => new { n.IdempotencyKey, n.CreatedDate })
                .FirstOrDefaultAsync(ct);

            DateTime? goalAtUtc = goalEvent?.CreatedDate is { } goalAt && hasConfirmedGoal
                && utcNow - BusinessDateHelper.EnsureUtc(goalAt) <= RecentWindow
                ? BusinessDateHelper.EnsureUtc(goalAt)
                : null;

            var latestPlacementSubmittedAt = placementAttempts
                .Select(a => a.SubmittedAt)
                .FirstOrDefault(s => s.HasValue);

            var placementAfterGoal = goalAtUtc.HasValue && latestPlacementSubmittedAt is { } pAt
                && BusinessDateHelper.EnsureUtc(pAt) > goalAtUtc.Value;

            if (goalAtUtc.HasValue && !roadmapAlreadyInitialized && !placementAfterGoal)
            {
                var nextStep = compatibility.Status == "COMPATIBLE"
                    ? "roadmap"
                    : compatibility.Status == "INSUFFICIENT_FOR_TARGET"
                        ? "supplement_placement"
                        : user.PlacementDecision == PlacementDecision.Skipped
                            ? "roadmap"
                            : "placement";

                var data = new Dictionary<string, string>
                {
                    ["targetLevel"] = user.TargetCertificateLevel?.Code ?? string.Empty,
                    ["nextStep"] = nextStep,
                    ["compatibilityStatus"] = compatibility.Status,
                };
                if (!string.IsNullOrWhiteSpace(compatibility.Reason))
                    data["compatibilityReason"] = compatibility.Reason;
                if (!string.IsNullOrWhiteSpace(compatibility.CoveredLevelName))
                    data["coveredLevel"] = compatibility.CoveredLevelName;
                if (user.DailyStudyMinutesMin is > 0 && user.DailyStudyMinutesMax > user.DailyStudyMinutesMin)
                {
                    data["dailyMinutesMin"] = user.DailyStudyMinutesMin.Value.ToString(CultureInfo.InvariantCulture);
                    data["dailyMinutesMax"] = user.DailyStudyMinutesMax!.Value.ToString(CultureInfo.InvariantCulture);
                }
                else if (user.DailyStudyMinutes > 0)
                {
                    data["dailyMinutes"] = user.DailyStudyMinutes.ToString(CultureInfo.InvariantCulture);
                }
                if (user.TargetExamDate is { } examDate)
                    data["examDate"] = examDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("goal", userId, goalEvent!.IdempotencyKey![goalPrefix.Length..]),
                    Type = "GOAL_CONFIRMED",
                    Level = "major",
                    Priority = 30,
                    AchievedAtUtc = goalAtUtc,
                    CtaHref = nextStep == "roadmap" ? "/roadmap/" : "/placement-test/",
                    Data = data,
                }));
            }
            // Legacy welcome for new accounts whose goal predates journey events (never alongside GOAL_CONFIRMED).
            else if (goalEvent is null && hasConfirmedGoal && user.LearningGoalVersion > 0
                && utcNow - user.CreatedDate <= NewAccountWindow && !roadmapAlreadyInitialized)
            {
                var next = compatibility.Status == "COMPATIBLE" || user.PlacementDecision == PlacementDecision.Skipped
                    ? "/roadmap/"
                    : "/placement-test/";
                var nextStep = compatibility.Status == "COMPATIBLE" || user.PlacementDecision == PlacementDecision.Skipped
                    ? "roadmap"
                    : "placement";
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("onboarding", userId),
                    Type = "ONBOARDING_COMPLETED",
                    Level = "major",
                    Priority = 30,
                    AchievedAtUtc = user.CreatedDate,
                    CtaHref = next,
                    Data = new Dictionary<string, string>
                    {
                        ["targetLevel"] = user.TargetCertificateLevel?.Code ?? string.Empty,
                        ["nextStep"] = nextStep,
                        ["compatibilityStatus"] = compatibility.Status,
                    },
                }));
            }

            // 2) Placement completed: submitted + graded attempt.
            //    Skip emitting a duplicate placement popup if GOAL_CONFIRMED happened AFTER this placement
            //    (Case 3 on GOAL_CONFIRMED already celebrates completion of the final prerequisite).
            var placement = placementAttempts.FirstOrDefault();
            var goalConfirmedAfterPlacement = goalAtUtc.HasValue && placement?.SubmittedAt is { } subAt
                && goalAtUtc.Value > BusinessDateHelper.EnsureUtc(subAt);
            if (placement is not null
                && utcNow - BusinessDateHelper.EnsureUtc(placement.SubmittedAt!.Value) <= RecentWindow
                && !roadmapAlreadyInitialized
                && !goalConfirmedAfterPlacement)
            {
                var assessedCode = user.AssessedCertificateLevel?.Code
                    ?? compatibility.CompatibleResult?.AssessedJlptLevelName
                    ?? compatibility.LatestResult?.AssessedJlptLevelName;
                var nextStep = !hasConfirmedGoal
                    ? "goal"
                    : compatibility.Status == "COMPATIBLE"
                        ? "roadmap"
                        : "supplement_placement";
                var data = new Dictionary<string, string>
                {
                    ["attemptId"] = placement.Id.ToString(CultureInfo.InvariantCulture),
                    ["hasGoal"] = hasConfirmedGoal ? "true" : "false",
                    ["nextStep"] = nextStep,
                    ["compatibilityStatus"] = compatibility.Status,
                };
                if (!string.IsNullOrWhiteSpace(assessedCode))
                    data["assessedLevel"] = assessedCode;
                if (hasConfirmedGoal && !string.IsNullOrWhiteSpace(user.TargetCertificateLevel?.Code))
                    data["targetLevel"] = user.TargetCertificateLevel.Code;
                if (!string.IsNullOrWhiteSpace(compatibility.Reason))
                    data["compatibilityReason"] = compatibility.Reason;
                if (!string.IsNullOrWhiteSpace(compatibility.CoveredLevelName))
                    data["coveredLevel"] = compatibility.CoveredLevelName;

                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("placement", userId, placement.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "PLACEMENT_COMPLETED",
                    Level = "major",
                    Priority = 25,
                    AchievedAtUtc = BusinessDateHelper.EnsureUtc(placement.SubmittedAt.Value),
                    CtaHref = nextStep == "goal"
                        ? "/onboarding/"
                        : nextStep == "roadmap"
                            ? "/roadmap/"
                            : "/placement-test/",
                    Data = data,
                }));
            }

            // 3) First lesson completed (once per learner).
            var firstLesson = await unitOfWork.LessonProgresses.Query()
                .AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted && p.IsCompleted && p.CompletedAt != null)
                .OrderBy(p => p.CompletedAt)
                .Select(p => new { p.LessonId, p.CompletedAt, p.Lesson.Title })
                .FirstOrDefaultAsync(ct);
            if (firstLesson is not null && utcNow - firstLesson.CompletedAt!.Value <= RecentWindow)
            {
                var hasPractice = await unitOfWork.PracticeExercises.Query()
                    .AsNoTracking()
                    .AnyAsync(e => e.LessonId == firstLesson.LessonId && !e.IsDeleted, ct);
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("first-lesson", userId),
                    Type = "FIRST_LESSON_COMPLETED",
                    Level = "major",
                    Priority = 34,
                    AchievedAtUtc = firstLesson.CompletedAt,
                    CtaHref = hasPractice ? $"/lesson-practice?lessonId={firstLesson.LessonId}" : "/lessons",
                    Data = new Dictionary<string, string>
                    {
                        ["lessonTitle"] = firstLesson.Title ?? string.Empty,
                        ["hasPractice"] = hasPractice ? "true" : "false",
                    },
                }));
            }

            // 4) Roadmap ready. AI: an active, confirmed personalised path (built from the goal + a real placement).
            //    Standard: the learner explicitly chose the standard roadmap (journey event) AND the roadmap loads
            //    with real lessons. Nothing is offered for a path that is still generating or failed.
            if (activePath?.ConfirmedAt is { } pathConfirmedAt && utcNow - BusinessDateHelper.EnsureUtc(pathConfirmedAt) <= RecentWindow)
            {
                var data = new Dictionary<string, string> { ["kind"] = "ai" };
                if (!string.IsNullOrWhiteSpace(user.TargetCertificateLevel?.Code))
                    data["targetLevel"] = user.TargetCertificateLevel.Code;
                AddAiPathFacts(activePath.SuggestionJson, data);
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("learning-path", userId, activePath.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "LEARNING_PATH_CREATED",
                    Level = "major",
                    Priority = 20,
                    AchievedAtUtc = BusinessDateHelper.EnsureUtc(pathConfirmedAt),
                    CtaHref = "/roadmap/",
                    Data = data,
                }));
            }
            else if (activePath is null && hasConfirmedGoal &&
                     (user.PlacementDecision == PlacementDecision.Skipped || standardEvent is not null))
            {
                if (standardEvent?.CreatedDate is { } standardAt && utcNow - BusinessDateHelper.EnsureUtc(standardAt) <= RecentWindow)
                {
                    var roadmap = await standardRoadmapService.GetAsync(ct);
                    if (roadmap.IsSuccess && roadmap.Result is { } r && (r.TotalLessons > 0 || r.Lessons.Count > 0))
                    {
                        var data = new Dictionary<string, string> { ["kind"] = "standard" };
                        var level = r.TargetJlptLevelName ?? user.TargetCertificateLevel?.Code;
                        if (!string.IsNullOrWhiteSpace(level)) data["targetLevel"] = level;
                        if (r.Stages.Count > 0) data["stages"] = r.Stages.Count.ToString(CultureInfo.InvariantCulture);
                        var lessons = r.TotalLessons > 0 ? r.TotalLessons : r.Lessons.Count;
                        data["lessons"] = lessons.ToString(CultureInfo.InvariantCulture);
                        if (r.DailyStudyMinutes > 0) data["dailyMinutes"] = r.DailyStudyMinutes.ToString(CultureInfo.InvariantCulture);
                        candidates.Add(new MomentCandidate(new LearningMomentResponse
                        {
                            OccurrenceKey = Key("standard-roadmap", userId, standardEvent.IdempotencyKey![standardPrefix.Length..]),
                            Type = "LEARNING_PATH_CREATED",
                            Level = "major",
                            Priority = 20,
                            AchievedAtUtc = BusinessDateHelper.EnsureUtc(standardAt),
                            CtaHref = "/roadmap/",
                            Data = data,
                        }));
                    }
                }
            }

            // 5) Mock test completed: submitted + graded (score is the real stored score).
            var mock = await unitOfWork.MockTestAttempts.Query()
                .AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsDeleted
                    && a.AttemptKind == AttemptKind.MockTest && !a.IsPlacementAssessment
                    && a.IsCompleted && a.SubmittedAt != null && a.TotalScore != null)
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefaultAsync(ct);
            if (mock is not null && utcNow - mock.SubmittedAt!.Value <= RecentWindow)
            {
                var scoreDisplay = $"{Math.Round(mock.TotalScore!.Value, 1).ToString(CultureInfo.InvariantCulture)}%";
                if (!string.IsNullOrWhiteSpace(mock.QuestionSnapshotJson))
                {
                    try
                    {
                        using var snapDoc = System.Text.Json.JsonDocument.Parse(mock.QuestionSnapshotJson);
                        if (snapDoc.RootElement.TryGetProperty("scoring", out var sc) &&
                            sc.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            sc.TryGetProperty("normalizedScore", out var normProp) &&
                            sc.TryGetProperty("normalizedMaxScore", out var maxProp) &&
                            normProp.TryGetDouble(out var normVal) &&
                            maxProp.TryGetInt32(out var maxVal) &&
                            maxVal > 0)
                        {
                            scoreDisplay = $"{MockTestScoringPolicy.FormatScore(normVal)}/{maxVal} điểm";
                        }
                    }
                    catch (System.Text.Json.JsonException) { }
                }

                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("mock-test", userId, mock.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "MOCK_TEST_COMPLETED",
                    Level = "milestone",
                    Priority = 50,
                    AchievedAtUtc = mock.SubmittedAt,
                    CtaHref = "/exam-result/",
                    Data = new Dictionary<string, string>
                    {
                        ["score"] = scoreDisplay,
                    },
                }));
            }

            // 6) Daily scheduled SRS done: a scheduled session finished today (VN study day) AND nothing is due now.
            //    Free practice sessions never count.
            var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow);
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(studyToday, DateTimeKind.Unspecified), SrsSchedulingHelper.StudyTimeZone);
            var finishedScheduledToday = await unitOfWork.SrsReviewSessions.Query()
                .AsNoTracking()
                .AnyAsync(s => s.UserId == userId && s.SessionKind == "Scheduled" && s.Status == "Completed"
                    && s.CompletedAtUtc != null && s.CompletedAtUtc >= todayStartUtc, ct);
            if (finishedScheduledToday)
            {
                var summary = await reviewService.GetSummaryAsync(userId, ct);
                if (summary.IsSuccess && summary.Result is { } s && s.TotalDueCards == 0 && s.ReviewedTodayCount > 0)
                {
                    candidates.Add(new MomentCandidate(new LearningMomentResponse
                    {
                        OccurrenceKey = Key("srs-daily", userId, studyToday.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                        Type = "DAILY_SRS_COMPLETED",
                        Level = "milestone",
                        Priority = 60,
                        AchievedAtUtc = utcNow,
                        CtaHref = "/daily-srs/",
                        Data = new Dictionary<string, string>
                        {
                            ["reviewedToday"] = s.ReviewedTodayCount.ToString(CultureInfo.InvariantCulture),
                        },
                    }));
                }
            }

            // 7) Passed Group Quiz & Stage Assessment checkpoints (server-verified PracticeExerciseAttempts only).
            var recentCheckpoints = await unitOfWork.PracticeExerciseAttempts.Query()
                .AsNoTracking()
                .Where(a => !a.IsDeleted
                    && a.UserId == userId
                    && a.HomeworkAssignmentId == null
                    && a.IsCompleted
                    && a.SubmittedAt != null
                    && a.ScorePercent.HasValue
                    && (a.PracticeExercise.Scope == PracticeExerciseScopes.GroupQuiz || a.PracticeExercise.Scope == PracticeExerciseScopes.StageAssessment)
                    && a.ScorePercent >= (a.PracticeExercise.PassingScorePercent > 0 ? a.PracticeExercise.PassingScorePercent : LessonPracticeProgress.PassingScore))
                .OrderByDescending(a => a.SubmittedAt)
                .Select(a => new
                {
                    a.PracticeExerciseId,
                    a.PracticeExercise.Scope,
                    a.PracticeExercise.Title,
                    ScorePercent = a.ScorePercent!.Value,
                    SubmittedAt = a.SubmittedAt!.Value,
                })
                .Take(5)
                .ToListAsync(ct);

            foreach (var cp in recentCheckpoints.DistinctBy(x => x.PracticeExerciseId))
            {
                if (utcNow - BusinessDateHelper.EnsureUtc(cp.SubmittedAt) > RecentWindow) continue;
                var isStage = cp.Scope == PracticeExerciseScopes.StageAssessment;
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key(isStage ? "stage-assessment" : "group-quiz", userId, cp.PracticeExerciseId.ToString(CultureInfo.InvariantCulture)),
                    Type = isStage ? "STAGE_ASSESSMENT_PASSED" : "GROUP_QUIZ_PASSED",
                    Level = isStage ? "major" : "milestone",
                    Priority = isStage ? 38 : 42,
                    AchievedAtUtc = cp.SubmittedAt,
                    CtaHref = "/roadmap",
                    Data = new Dictionary<string, string>
                    {
                        ["title"] = cp.Title,
                        ["score"] = Math.Round(cp.ScorePercent, 1).ToString(CultureInfo.InvariantCulture),
                    },
                }));
            }

            // 8) Purchase success: only orders the payment flow has marked Paid AND activated (StartDate set by
            //    SubscriptionAccessService.Activate after PayOS confirmation). Pending/Failed/Cancelled/Expired orders and
            //    redirect query strings never qualify. One moment per order (key = order id), claimed once via Notification.
            var recentPurchases = await unitOfWork.UserSubscriptions.Query()
                .AsNoTracking()
                .Include(s => s.SubscriptionPlan)
                    .ThenInclude(p => p!.Features)
                .Where(s => s.UserId == userId && !s.IsDeleted
                    && s.PaymentStatus == PaymentStatus.Paid
                    && s.StartDate != null
                    && s.MeetingSessionsIncluded == 0)
                .OrderByDescending(s => s.StartDate)
                .Take(5)
                .ToListAsync(ct);
            foreach (var order in recentPurchases)
            {
                var activatedAt = BusinessDateHelper.EnsureUtc(order.StartDate!.Value);
                if (utcNow - activatedAt > RecentWindow) continue;
                var category = SubscriptionAccessService.OrderCategoryPublic(order);
                if (category == SubscriptionAccessService.CategoryMentorLegacy) continue;
                var data = new Dictionary<string, string>
                {
                    ["planName"] = order.PlanNameSnapshot ?? order.SubscriptionPlan?.Name ?? string.Empty,
                    ["kind"] = category,
                };
                if (order.EndDate is { } end && !SubscriptionAccessService.IsUnlimitedExpiry(end))
                    data["endDate"] = BusinessDateHelper.EnsureUtc(end).ToString("O", CultureInfo.InvariantCulture);
                else if (order.EndDate is { } && SubscriptionAccessService.IsUnlimitedExpiry(order.EndDate))
                    data["isUnlimited"] = "true";
                if (order.DurationDaysSnapshot is > 0)
                    data["durationDays"] = order.DurationDaysSnapshot.Value.ToString(CultureInfo.InvariantCulture);
                if (order.AiGradingQuota > 0)
                    data["aiUses"] = order.AiGradingQuota.ToString(CultureInfo.InvariantCulture);
                // Renewal / top-up: another paid order was already active when this one activated, so the copy says
                // "extended" / "credits added" and only features not already owned are marked as newly unlocked.
                var earlierActive = await unitOfWork.UserSubscriptions.Query()
                    .AsNoTracking()
                    .Include(s => s.SubscriptionPlan)
                        .ThenInclude(p => p!.Features)
                    .Where(s => s.UserId == userId && s.Id != order.Id && !s.IsDeleted
                        && s.PaymentStatus == PaymentStatus.Paid && s.StartDate != null && s.StartDate < order.StartDate
                        && (s.EndDate == null || s.EndDate > order.StartDate)
                        && s.MeetingSessionsIncluded == 0)
                    .ToListAsync(ct);
                var previouslyGranted = earlierActive
                    .Where(s => s.EndDate != null)
                    .SelectMany(s => s.SubscriptionPlan?.Features ?? [])
                    .Select(f => f.Code)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var isCredit = SubscriptionAccessService.IsCreditCategory(category);
                var hadSameCategory = earlierActive.Any(s =>
                    string.Equals(SubscriptionAccessService.OrderCategoryPublic(s), category, StringComparison.OrdinalIgnoreCase)
                    && (isCredit || s.EndDate != null));
                data["mode"] = isCredit ? (hadSameCategory ? "topup" : "credit") : hadSameCategory ? "renewal" : "activation";
                var benefits = (order.SubscriptionPlan?.Features ?? [])
                    .Where(f => !f.IsDeleted)
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new LearningMomentBenefitResponse
                    {
                        Code = f.Code,
                        Name = f.Name,
                        Description = f.Description,
                        Category = f.Category,
                        IsNew = !previouslyGranted.Contains(f.Code),
                    })
                    .Take(8)
                    .ToList();
                // The CTA leads to the first step the learner can really take: set a goal first, then the placement
                // test for the AI roadmap — never to a page that would answer with an error.
                var ctaHref = category switch
                {
                    SubscriptionAccessService.CategorySelfStudy or SubscriptionAccessService.CategoryCombo => "/lessons/",
                    SubscriptionAccessService.CategoryLearningPath => "/roadmap/",
                    SubscriptionAccessService.CategorySpeaking => "/riki/speaking/",
                    SubscriptionAccessService.CategoryWriting => "/riki/essay/",
                    _ => "/plans/",
                };
                var needsGoal = !user.TargetCertificateLevelId.HasValue;
                if (needsGoal && category is SubscriptionAccessService.CategorySelfStudy or SubscriptionAccessService.CategoryCombo or SubscriptionAccessService.CategoryLearningPath)
                {
                    ctaHref = "/onboarding/";
                    data["ctaStep"] = "goal";
                }
                else if (category == SubscriptionAccessService.CategoryLearningPath
                    && user.PlacementDecision != PlacementDecision.Completed && activePath is null)
                {
                    ctaHref = "/placement-test/";
                    data["ctaStep"] = "placement";
                }
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    Benefits = benefits,
                    OccurrenceKey = Key("purchase", userId, order.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "PURCHASE_COMPLETED",
                    Level = "major",
                    // Tied to what the learner just did, so it goes before learning milestones in the queue.
                    Priority = 5,
                    AchievedAtUtc = activatedAt,
                    CtaHref = ctaHref,
                    Data = data,
                }));
            }

            return candidates;
        }

        private async Task<HashSet<string>> ClaimedKeysAsync(int userId, IEnumerable<string> keys, CancellationToken ct)
        {
            var list = keys.ToList();
            if (list.Count == 0) return [];
            var claimed = await unitOfWork.Notifications.Query()
                .AsNoTracking()
                .Where(n => n.UserId == userId && n.IdempotencyKey != null && list.Contains(n.IdempotencyKey))
                .Select(n => n.IdempotencyKey!)
                .ToListAsync(ct);
            return claimed.ToHashSet(StringComparer.Ordinal);
        }

        public async Task<ApiResponse<PendingLearningMomentsResponse>> GetPendingAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var utcNow = DateTime.UtcNow;
                var candidates = await BuildCandidatesAsync(userId, utcNow, cancellationToken);
                var claimed = await ClaimedKeysAsync(userId, candidates.Select(c => c.Moment.OccurrenceKey), cancellationToken);
                return ApiResponse<PendingLearningMomentsResponse>.Success(new PendingLearningMomentsResponse
                {
                    Items = candidates
                        .Where(c => !claimed.Contains(c.Moment.OccurrenceKey))
                        .Select(c => c.Moment)
                        .OrderBy(m => m.Priority)
                        .ToList(),
                    ServerUtcNow = utcNow,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<PendingLearningMomentsResponse>.Fail(
                    "Không thể tải khoảnh khắc học tập.", HttpStatusCode.InternalServerError, errors: [ex.GetType().Name]);
            }
        }

        public async Task<ApiResponse<ClaimLearningMomentResponse>> ClaimAsync(ClaimLearningMomentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var key = request.OccurrenceKey?.Trim() ?? string.Empty;
                var candidates = await BuildCandidatesAsync(userId, DateTime.UtcNow, cancellationToken);
                // Server re-validates eligibility: a forged/stale key (or another learner's key) is never claimable.
                var match = candidates.FirstOrDefault(c => string.Equals(c.Moment.OccurrenceKey, key, StringComparison.Ordinal));
                if (match is null)
                    return ApiResponse<ClaimLearningMomentResponse>.Success(new ClaimLearningMomentResponse { Claimed = false });

                var alreadyClaimed = await unitOfWork.Notifications.AnyAsync(n => n.UserId == userId && n.IdempotencyKey == key, cancellationToken);
                if (alreadyClaimed)
                    return ApiResponse<ClaimLearningMomentResponse>.Success(new ClaimLearningMomentResponse { Claimed = false, Moment = match.Moment });

                var notification = new Notification
                {
                    UserId = userId,
                    Title = TitleFor(match.Moment),
                    Message = "Một cột mốc mới trong hành trình học tiếng Nhật của bạn.",
                    Type = MomentNotificationType,
                    // Shown in the celebration dialog itself: history only, no unread badge or push.
                    IsRead = true,
                    LinkUrl = match.Moment.CtaHref,
                    IdempotencyKey = key,
                    CreatedDate = DateTime.UtcNow,
                };
                try
                {
                    await unitOfWork.Notifications.AddAsync(notification, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Another tab/device won the unique IdempotencyKey race.
                    return ApiResponse<ClaimLearningMomentResponse>.Success(new ClaimLearningMomentResponse { Claimed = false, Moment = match.Moment });
                }

                return ApiResponse<ClaimLearningMomentResponse>.Success(new ClaimLearningMomentResponse { Claimed = true, Moment = match.Moment });
            }
            catch (Exception ex)
            {
                return ApiResponse<ClaimLearningMomentResponse>.Fail(
                    "Không thể ghi nhận khoảnh khắc học tập.", HttpStatusCode.InternalServerError, errors: [ex.GetType().Name]);
            }
        }

        /// <summary>Copies display-only facts (phase count, plan length) from a stored AI path; ignores bad JSON.</summary>
        private static void AddAiPathFacts(string? suggestionJson, Dictionary<string, string> data)
        {
            if (string.IsNullOrWhiteSpace(suggestionJson)) return;
            try
            {
                using var doc = JsonDocument.Parse(suggestionJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                if (TryGetProperty(root, "phases", out var phases) && phases.ValueKind == JsonValueKind.Array && phases.GetArrayLength() > 0)
                    data["stages"] = phases.GetArrayLength().ToString(CultureInfo.InvariantCulture);
                if (TryGetProperty(root, "planDurationDays", out var days) && days.ValueKind == JsonValueKind.Number && days.TryGetInt32(out var d) && d > 0)
                    data["planDays"] = d.ToString(CultureInfo.InvariantCulture);
                if (TryGetProperty(root, "dailyStudyMinutes", out var mins) && mins.ValueKind == JsonValueKind.Number && mins.TryGetInt32(out var m) && m > 0)
                    data["dailyMinutes"] = m.ToString(CultureInfo.InvariantCulture);
            }
            catch (JsonException)
            {
                // A celebration never fails because of stored path formatting.
            }
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static string TitleFor(LearningMomentResponse moment) => moment.Type switch
        {
            "GOAL_CONFIRMED" => "Mục tiêu JLPT đã được thiết lập",
            "PURCHASE_COMPLETED" => "Gói học của bạn đã được kích hoạt",
            "ONBOARDING_COMPLETED" => "Chào mừng bạn đến với hành trình tiếng Nhật!",
            "PLACEMENT_COMPLETED" => "Bạn đã hoàn thành bài đánh giá năng lực!",
            "FIRST_LESSON_COMPLETED" => "Bài học đầu tiên đã hoàn thành!",
            "LEARNING_PATH_CREATED" => "Lộ trình học của bạn đã sẵn sàng!",
            "MOCK_TEST_COMPLETED" => "Bạn đã hoàn thành một bài thi thử!",
            "DAILY_SRS_COMPLETED" => "Bạn đã hoàn thành phiên ôn hôm nay!",
            _ => "Khoảnh khắc học tập mới",
        };

        public async Task<ApiResponse<LearnerAchievementsResponse>> GetAchievementsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var result = new LearnerAchievementsResponse();

                var user = await unitOfWork.UserAccounts.Query().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is null) return ApiResponse<LearnerAchievementsResponse>.Success(result);

                if (user.TargetCertificateLevelId.HasValue && user.LearningGoalVersion > 0)
                    result.Achieved.Add(new LearnerAchievementResponse { Code = "ONBOARDING_COMPLETED", Category = "journey", AchievedAtUtc = user.CreatedDate });

                var firstPlacementAt = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                    .Where(a => a.UserId == userId && !a.IsDeleted && (a.AttemptKind == AttemptKind.Placement || a.IsPlacementAssessment)
                        && a.IsCompleted && a.SubmittedAt != null && a.TotalScore != null)
                    .MinAsync(a => (DateTime?)a.SubmittedAt, cancellationToken);
                if (firstPlacementAt is not null)
                    result.Achieved.Add(new LearnerAchievementResponse { Code = "PLACEMENT_COMPLETED", Category = "journey", AchievedAtUtc = firstPlacementAt });

                var firstPathAt = await unitOfWork.RecommendedLearningPaths.Query().AsNoTracking()
                    .Where(p => p.UserId == userId && !p.IsDeleted && p.ConfirmedAt != null)
                    .MinAsync(p => p.ConfirmedAt, cancellationToken);
                if (firstPathAt is not null)
                    result.Achieved.Add(new LearnerAchievementResponse { Code = "LEARNING_PATH_CREATED", Category = "journey", AchievedAtUtc = firstPathAt });

                // Lesson count badges with the real date each count was reached.
                var lessonDates = await unitOfWork.LessonProgresses.Query().AsNoTracking()
                    .Where(p => p.UserId == userId && !p.IsDeleted && p.IsCompleted && p.CompletedAt != null)
                    .OrderBy(p => p.CompletedAt)
                    .Select(p => p.CompletedAt!.Value)
                    .ToListAsync(cancellationToken);
                foreach (var n in LessonCountBadges)
                {
                    if (lessonDates.Count >= n)
                        result.Achieved.Add(new LearnerAchievementResponse { Code = n == 1 ? "FIRST_LESSON_COMPLETED" : "LESSONS_COMPLETED", Category = "lesson", Value = n, AchievedAtUtc = lessonDates[n - 1] });
                }
                var nextLessonBadge = LessonCountBadges.FirstOrDefault(n => n > lessonDates.Count);
                if (nextLessonBadge > 0)
                    result.Next.Add(new LearnerAchievementResponse { Code = "LESSONS_COMPLETED", Category = "lesson", Value = nextLessonBadge });

                var firstMockAt = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                    .Where(a => a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest && !a.IsPlacementAssessment
                        && a.IsCompleted && a.SubmittedAt != null && a.TotalScore != null)
                    .MinAsync(a => (DateTime?)a.SubmittedAt, cancellationToken);
                if (firstMockAt is not null)
                    result.Achieved.Add(new LearnerAchievementResponse { Code = "MOCK_TEST_COMPLETED", Category = "exam", AchievedAtUtc = firstMockAt });

                // Streak badges are earned once the longest real streak reaches the milestone (badge kept permanently).
                var streak = await analyticsService.GetStudyStreakAsync(cancellationToken);
                var longest = streak.IsSuccess && streak.Result is not null ? streak.Result.LongestStreakDays : 0;
                foreach (var m in AnalyticsService.StreakMilestones.Where(m => m <= longest))
                    result.Achieved.Add(new LearnerAchievementResponse { Code = "STREAK_MILESTONE", Category = "streak", Value = m });
                var nextStreak = AnalyticsService.StreakMilestones.FirstOrDefault(m => m > longest);
                if (nextStreak > 0)
                    result.Next.Add(new LearnerAchievementResponse { Code = "STREAK_MILESTONE", Category = "streak", Value = nextStreak });

                result.Achieved = result.Achieved
                    .OrderByDescending(a => a.AchievedAtUtc ?? DateTime.MinValue)
                    .ThenByDescending(a => a.Value ?? 0)
                    .ToList();
                return ApiResponse<LearnerAchievementsResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<LearnerAchievementsResponse>.Fail(
                    "Không thể tải thành tích.", HttpStatusCode.InternalServerError, errors: [ex.GetType().Name]);
            }
        }
    }
}
