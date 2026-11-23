using System.Globalization;
using System.Net;
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
        IAnalyticsService analyticsService) : ILearningMomentService
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

            // 1) Onboarding completed: the learning goal is saved (server-validated write). There is no completion
            //    timestamp, so only new accounts qualify — existing learners never get a retroactive welcome.
            if (user.TargetCertificateLevelId.HasValue && user.LearningGoalVersion > 0 && utcNow - user.CreatedDate <= NewAccountWindow)
            {
                var next = user.PlacementDecision == PlacementDecision.NotTaken
                    ? "/placement"
                    : activePath is not null ? "/roadmap" : "/lessons";
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("onboarding", userId),
                    Type = "ONBOARDING_COMPLETED",
                    Level = "major",
                    Priority = 10,
                    AchievedAtUtc = user.CreatedDate,
                    CtaHref = next,
                    Data = new Dictionary<string, string>
                    {
                        ["targetLevel"] = user.TargetCertificateLevel?.Code ?? string.Empty,
                        ["nextStep"] = user.PlacementDecision == PlacementDecision.NotTaken ? "placement" : activePath is not null ? "roadmap" : "lessons",
                    },
                }));
            }

            // 2) Placement completed: submitted + graded attempt (each real retake is its own occurrence).
            var placement = await unitOfWork.MockTestAttempts.Query()
                .AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsDeleted
                    && (a.AttemptKind == AttemptKind.Placement || a.IsPlacementAssessment)
                    && a.IsCompleted && a.SubmittedAt != null && a.TotalScore != null)
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefaultAsync(ct);
            if (placement is not null && utcNow - placement.SubmittedAt!.Value <= RecentWindow)
            {
                var data = new Dictionary<string, string> { ["attemptId"] = placement.Id.ToString(CultureInfo.InvariantCulture) };
                if (!string.IsNullOrWhiteSpace(user.AssessedCertificateLevel?.Code))
                    data["assessedLevel"] = user.AssessedCertificateLevel.Code;
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("placement", userId, placement.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "PLACEMENT_COMPLETED",
                    Level = "major",
                    Priority = 20,
                    AchievedAtUtc = placement.SubmittedAt,
                    CtaHref = $"/placement/result/{placement.Id}",
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
                    Priority = 30,
                    AchievedAtUtc = firstLesson.CompletedAt,
                    CtaHref = hasPractice ? $"/lesson-practice?lessonId={firstLesson.LessonId}" : "/lessons",
                    Data = new Dictionary<string, string>
                    {
                        ["lessonTitle"] = firstLesson.Title ?? string.Empty,
                        ["hasPractice"] = hasPractice ? "true" : "false",
                    },
                }));
            }

            // 4) Learning path ready (active + confirmed). An AI path is only claimed as AI when it is one.
            if (activePath is not null)
            {
                var readyAt = activePath.ConfirmedAt ?? activePath.GeneratedAt;
                if (activePath.ConfirmedAt is not null && utcNow - readyAt <= RecentWindow)
                {
                    candidates.Add(new MomentCandidate(new LearningMomentResponse
                    {
                        OccurrenceKey = Key("learning-path", userId, activePath.Id.ToString(CultureInfo.InvariantCulture)),
                        Type = "LEARNING_PATH_CREATED",
                        Level = "milestone",
                        Priority = 35,
                        AchievedAtUtc = readyAt,
                        CtaHref = "/roadmap",
                        Data = new Dictionary<string, string> { ["kind"] = "personalized" },
                    }));
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
                        ["score"] = Math.Round(mock.TotalScore!.Value, 1).ToString(CultureInfo.InvariantCulture),
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
                if (order.EndDate is { } end)
                    data["endDate"] = BusinessDateHelper.EnsureUtc(end).ToString("O", CultureInfo.InvariantCulture);
                if (order.DurationDaysSnapshot is > 0)
                    data["durationDays"] = order.DurationDaysSnapshot.Value.ToString(CultureInfo.InvariantCulture);
                if (order.AiGradingQuota > 0)
                    data["aiUses"] = order.AiGradingQuota.ToString(CultureInfo.InvariantCulture);
                candidates.Add(new MomentCandidate(new LearningMomentResponse
                {
                    OccurrenceKey = Key("purchase", userId, order.Id.ToString(CultureInfo.InvariantCulture)),
                    Type = "PURCHASE_COMPLETED",
                    Level = "major",
                    // Tied to what the learner just did, so it goes before learning milestones in the queue.
                    Priority = 5,
                    AchievedAtUtc = activatedAt,
                    CtaHref = category switch
                    {
                        SubscriptionAccessService.CategorySelfStudy => "/lessons/",
                        SubscriptionAccessService.CategoryLearningPath => "/roadmap/",
                        SubscriptionAccessService.CategorySpeaking => "/riki/speaking/",
                        SubscriptionAccessService.CategoryWriting => "/riki/essay/",
                        _ => "/plans/",
                    },
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

        private static string TitleFor(LearningMomentResponse moment) => moment.Type switch
        {
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
