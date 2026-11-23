using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Analytics;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Analytics;
using System.Net;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    public class AnalyticsService(IUnitOfWork unitOfWork, IClaimService claimService) : IAnalyticsService
    {
        private static DateTime StudyDate(DateTime utc) =>
            SrsSchedulingHelper.GetStudyDateInZone(utc);

        /// <summary>
        /// Streak milestones (days). Mirrors the learner UI milestone chain; the server decides which event a day earns.
        /// </summary>
        public static readonly IReadOnlyList<int> StreakMilestones = [3, 7, 14, 30, 60, 100];

        private const string StreakCelebrationType = "StreakCelebration";

        private sealed record StreakSnapshot(
            int Current,
            int Longest,
            DateTime? LastActivity,
            bool IsActiveToday,
            DateTime Today,
            HashSet<DateTime> ActivityDays);

        /// <summary>
        /// Single source of truth for streak values: valid learning activity = lesson progress, SRS review history,
        /// or a mock test attempt, bucketed into Asia/Ho_Chi_Minh study days. Visiting pages never creates activity.
        /// </summary>
        private async Task<StreakSnapshot> ComputeStreakAsync(int userId)
        {
            var lessonDates = await unitOfWork.LessonProgresses.GetActivityDatesAsync(userId);
            var reviewDates = await unitOfWork.ReviewHistories.GetActivityDatesAsync(userId);
            var testDates = await unitOfWork.MockTestAttempts.GetActivityDatesAsync(userId);

            var allDates = lessonDates.Concat(reviewDates).Concat(testDates)
                .Select(StudyDate)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var today = StudyDate(DateTime.UtcNow);
            var dateSet = allDates.ToHashSet();
            if (allDates.Count == 0)
                return new StreakSnapshot(0, 0, null, false, today, dateSet);

            var lastActivity = allDates[0];

            // Current streak: count back from today (or yesterday when today has no activity yet) without gaps.
            var currentStreak = 0;
            var cursor = lastActivity == today ? today : (lastActivity == today.AddDays(-1) ? today.AddDays(-1) : (DateTime?)null);
            if (cursor is not null)
            {
                while (dateSet.Contains(cursor.Value))
                {
                    currentStreak++;
                    cursor = cursor.Value.AddDays(-1);
                }
            }

            // Longest streak: scan all (descending) study days for the longest consecutive run.
            var longestStreak = 1;
            var runLength = 1;
            for (var i = 1; i < allDates.Count; i++)
            {
                if (allDates[i - 1].AddDays(-1) == allDates[i])
                {
                    runLength++;
                    longestStreak = Math.Max(longestStreak, runLength);
                }
                else
                {
                    runLength = 1;
                }
            }

            return new StreakSnapshot(
                currentStreak,
                Math.Max(longestStreak, currentStreak),
                lastActivity,
                lastActivity == today,
                today,
                dateSet);
        }

        public async Task<ApiResponse<StudyStreakResponse>> GetStudyStreakAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var snapshot = await ComputeStreakAsync(userId);
                return ApiResponse<StudyStreakResponse>.Success(new StudyStreakResponse
                {
                    CurrentStreakDays = snapshot.Current,
                    LongestStreakDays = snapshot.Longest,
                    LastActivityDate = snapshot.LastActivity,
                    IsActiveToday = snapshot.IsActiveToday,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<StudyStreakResponse>.Fail(
                    "Không thể tính chuỗi ngày học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private const string StreakReminderType = "StreakReminder";

        /// <summary>
        /// Server-decided streak event for today (Asia/Ho_Chi_Minh), at most one of each kind per study day:
        /// - "daily"/"milestone": today already holds a verified learning activity (celebrate the extended streak);
        /// - "atRisk": a live streak (last activity yesterday) and nothing studied yet today (keep-the-streak reminder).
        /// Visiting pages never creates activity; the event kind only reflects real activity data.
        /// </summary>
        private static (string? Kind, string? EventId, string? IdempotencyKey) ResolveStreakEvent(StreakSnapshot snapshot, int userId)
        {
            if (snapshot.Current <= 0)
                return (null, null, null);
            var date = snapshot.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            if (snapshot.IsActiveToday)
            {
                var kind = StreakMilestones.Contains(snapshot.Current) ? "milestone" : "daily";
                return (kind, $"streak:{date}:{snapshot.Current}", $"streak-celebration:{userId}:{date}");
            }
            return ("atRisk", $"streak-risk:{date}:{snapshot.Current}", $"streak-reminder:{userId}:{date}");
        }

        private static DateTime StudyDayEndsAtUtc(DateTime studyDate) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(studyDate.Date.AddDays(1), DateTimeKind.Unspecified), SrsSchedulingHelper.StudyTimeZone);

        private static StreakCelebrationResponse BuildCelebration(StreakSnapshot snapshot, int userId, bool alreadyClaimed)
        {
            var monday = snapshot.Today.AddDays(-(((int)snapshot.Today.DayOfWeek + 6) % 7));
            var week = Enumerable.Range(0, 7)
                .Select(i => monday.AddDays(i))
                .Where(snapshot.ActivityDays.Contains)
                .ToList();

            var (kind, eventId, _) = ResolveStreakEvent(snapshot, userId);
            var hasEvent = kind is not null;

            return new StreakCelebrationResponse
            {
                HasPendingEvent = hasEvent && !alreadyClaimed,
                EventId = eventId,
                EventType = kind,
                StudyDate = hasEvent ? snapshot.Today : null,
                StudyDayEndsAtUtc = StudyDayEndsAtUtc(snapshot.Today),
                CurrentStreakDays = snapshot.Current,
                LongestStreakDays = snapshot.Longest,
                IsActiveToday = snapshot.IsActiveToday,
                Milestone = kind == "milestone" ? snapshot.Current : null,
                NextMilestone = StreakMilestones.Where(m => m > snapshot.Current).Cast<int?>().FirstOrDefault(),
                Milestones = [.. StreakMilestones],
                WeekActivityDates = week,
            };
        }

        private Task<bool> IsStreakEventClaimedAsync(int userId, string? key, CancellationToken cancellationToken) =>
            key is null
                ? Task.FromResult(false)
                : unitOfWork.Notifications.AnyAsync(n => n.UserId == userId && n.IdempotencyKey == key, cancellationToken);

        public async Task<ApiResponse<StreakCelebrationResponse>> GetStreakCelebrationAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var snapshot = await ComputeStreakAsync(userId);
                var (_, _, key) = ResolveStreakEvent(snapshot, userId);
                var claimed = await IsStreakEventClaimedAsync(userId, key, cancellationToken);
                return ApiResponse<StreakCelebrationResponse>.Success(BuildCelebration(snapshot, userId, claimed));
            }
            catch (Exception ex)
            {
                return ApiResponse<StreakCelebrationResponse>.Fail(
                    "Không thể kiểm tra sự kiện chuỗi ngày học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<StreakCelebrationResponse>> ClaimStreakCelebrationAsync(
            ClaimStreakCelebrationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var snapshot = await ComputeStreakAsync(userId);
                var (kind, _, key) = ResolveStreakEvent(snapshot, userId);
                var alreadyClaimed = await IsStreakEventClaimedAsync(userId, key, cancellationToken);
                var celebration = BuildCelebration(snapshot, userId, alreadyClaimed);

                // Stale or forged event (tab open across midnight, activity changed the kind, already claimed): nothing to claim.
                if (!celebration.HasPendingEvent
                    || key is null
                    || !string.Equals(celebration.EventId, request.EventId?.Trim(), StringComparison.Ordinal))
                {
                    celebration.HasPendingEvent = false;
                    return ApiResponse<StreakCelebrationResponse>.Success(celebration);
                }

                var isReminder = kind == "atRisk";
                var notification = new Notification
                {
                    UserId = userId,
                    Title = isReminder
                        ? $"Giữ chuỗi {snapshot.Current} ngày học"
                        : kind == "milestone"
                            ? $"Chạm mốc {snapshot.Current} ngày học liên tiếp!"
                            : $"Chuỗi {snapshot.Current} ngày học liên tiếp",
                    Message = isReminder
                        ? $"Hoàn thành 1 hoạt động học hôm nay để lên {snapshot.Current + 1} ngày."
                        : "Bạn đã duy trì thói quen học mỗi ngày. Tiếp tục giữ lửa nhé!",
                    Type = isReminder ? StreakReminderType : StreakCelebrationType,
                    // Already shown in the dialog: lands in history without an unread badge or push delivery.
                    IsRead = true,
                    LinkUrl = "/app/",
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
                    // Another tab/device won the unique IdempotencyKey race: this caller must not show the dialog.
                    celebration.HasPendingEvent = false;
                    return ApiResponse<StreakCelebrationResponse>.Success(celebration);
                }

                celebration.HasPendingEvent = false;
                celebration.Claimed = true;
                return ApiResponse<StreakCelebrationResponse>.Success(celebration);
            }
            catch (Exception ex)
            {
                return ApiResponse<StreakCelebrationResponse>.Fail(
                    "Không thể ghi nhận sự kiện chuỗi ngày học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<CompletionStatsResponse>> GetCompletionStatsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var (total, completed) = await unitOfWork.Lessons.GetCompletionStatsAsync(userId);
                var percent = total == 0 ? 0d : Math.Round(completed * 100d / total, 2);

                return ApiResponse<CompletionStatsResponse>.Success(new CompletionStatsResponse
                {
                    TotalLessons = total,
                    CompletedLessons = completed,
                    CompletionPercent = percent,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<CompletionStatsResponse>.Fail(
                    "Không thể tải thống kê tiến độ học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<LanguageSkillBreakdownItem>>> GetLanguageSkillBreakdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var breakdown = await unitOfWork.MockTestSectionResults.GetLanguageSkillBreakdownAsync(userId);

                var result = breakdown
                 .Where(b => b.MockTestSection?.LanguageSkill != null)
                 .GroupBy(b => b.MockTestSection.LanguageSkill.Name)
                 .Select(g => new LanguageSkillBreakdownItem
                 {
                     LanguageSkillName = g.Key,
                     AverageScore = Math.Round(g.Average(x => x.ScorePercent), 2),
                     AttemptCount = g.Count(),
                 })
                 .ToList();

                return ApiResponse<List<LanguageSkillBreakdownItem>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<LanguageSkillBreakdownItem>>.Fail(
                    "Không thể tải phân tích điểm mạnh/yếu.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<SkillMonthlyAnalyticsResponse>> GetMonthlySkillAnalyticsAsync(
            int months,
            CancellationToken cancellationToken)
        {
            if (months is < 1 or > 24)
                return ApiResponse<SkillMonthlyAnalyticsResponse>.Fail(
                    "Số tháng phải trong khoảng 1 đến 24.", HttpStatusCode.BadRequest);

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                var nowVn = DateTime.UtcNow.AddHours(7);
                var currentMonth = new DateTime(nowVn.Year, nowVn.Month, 1);
                var firstMonth = currentMonth.AddMonths(-(months - 1));
                var fromUtc = DateTime.SpecifyKind(firstMonth.AddHours(-7), DateTimeKind.Utc);
                var rows = await unitOfWork.MockTestSectionResults.GetSkillHistoryByUserSinceAsync(
                    userId, fromUtc, cancellationToken);

                var monthKeys = Enumerable.Range(0, months)
                    .Select(index => firstMonth.AddMonths(index).ToString("yyyy-MM"))
                    .ToList();
                var rowsWithSkill = rows
                    .Where(row => row.MockTestSection?.LanguageSkill is not null && row.TotalCount > 0)
                    .Select(row => new
                    {
                        SkillName = row.MockTestSection!.LanguageSkill!.Name,
                        row.CorrectCount,
                        row.TotalCount,
                        row.MockTestAttemptId,
                        AttemptKind = row.MockTestAttempt!.AttemptKind,
                        SubmittedAtVn = row.MockTestAttempt!.SubmittedAt!.Value.AddHours(7),
                    })
                    .ToList();

                var skillItems = rowsWithSkill
                    .GroupBy(row => row.SkillName)
                    .Select(skillGroup =>
                    {
                        var monthlyScores = monthKeys.Select(monthKey =>
                        {
                            var monthRows = skillGroup
                                .Where(row => row.SubmittedAtVn.ToString("yyyy-MM") == monthKey)
                                .ToList();
                            var questionCount = monthRows.Sum(row => row.TotalCount);
                            var correctCount = monthRows.Sum(row => row.CorrectCount);
                            return new SkillMonthlyScore
                            {
                                Month = monthKey,
                                ScorePercent = questionCount == 0
                                    ? null
                                    : Math.Round(correctCount * 100d / questionCount, 2),
                                AttemptCount = monthRows.Select(row => row.MockTestAttemptId).Distinct().Count(),
                                MockTestAttemptCount = monthRows
                                    .Where(row => row.AttemptKind == RikiPath.Domain.Enums.AttemptKind.MockTest)
                                    .Select(row => row.MockTestAttemptId).Distinct().Count(),
                                PlacementAttemptCount = monthRows
                                    .Where(row => row.AttemptKind == RikiPath.Domain.Enums.AttemptKind.Placement)
                                    .Select(row => row.MockTestAttemptId).Distinct().Count(),
                                QuestionCount = questionCount,
                            };
                        }).ToList();

                        var recordedMonths = monthlyScores.Where(score => score.ScorePercent.HasValue).ToList();
                        var current = recordedMonths.LastOrDefault();
                        var previous = recordedMonths.Count > 1 ? recordedMonths[^2] : null;
                        return new SkillMonthlyAnalyticsItem
                        {
                            LanguageSkillName = skillGroup.Key,
                            CurrentScorePercent = current?.ScorePercent,
                            CurrentAssessment = current?.ScorePercent is double currentScore
                                ? DescribeSkillScore(currentScore)
                                : "Chưa có dữ liệu",
                            ChangeFromPreviousRecordedMonth = current is not null && previous is not null
                                ? Math.Round(current.ScorePercent!.Value - previous.ScorePercent!.Value, 2)
                                : null,
                            ComparedMonth = previous?.Month,
                            TotalAttempts = skillGroup.Select(row => row.MockTestAttemptId).Distinct().Count(),
                            MockTestAttempts = skillGroup
                                .Where(row => row.AttemptKind == RikiPath.Domain.Enums.AttemptKind.MockTest)
                                .Select(row => row.MockTestAttemptId).Distinct().Count(),
                            PlacementAttempts = skillGroup
                                .Where(row => row.AttemptKind == RikiPath.Domain.Enums.AttemptKind.Placement)
                                .Select(row => row.MockTestAttemptId).Distinct().Count(),
                            MonthlyScores = monthlyScores,
                        };
                    })
                    .OrderByDescending(skill => skill.CurrentScorePercent ?? -1)
                    .ThenBy(skill => skill.LanguageSkillName)
                    .ToList();

                var latestMonthRows = rowsWithSkill
                    .GroupBy(row => row.SubmittedAtVn.ToString("yyyy-MM"))
                    .OrderByDescending(group => group.Key)
                    .FirstOrDefault();
                var latestQuestionCount = latestMonthRows?.Sum(row => row.TotalCount) ?? 0;
                var latestOverallScore = latestQuestionCount == 0
                    ? (double?)null
                    : Math.Round(latestMonthRows!.Sum(row => row.CorrectCount) * 100d / latestQuestionCount, 2);
                var strongest = skillItems.FirstOrDefault(skill => skill.CurrentScorePercent.HasValue);
                var weakest = skillItems.LastOrDefault(skill => skill.CurrentScorePercent.HasValue);

                var summary = latestOverallScore.HasValue
                    ? $"Tháng {latestMonthRows!.Key}, điểm tổng hợp theo kỹ năng là {latestOverallScore.Value:0.##}%. " +
                      (strongest is not null && weakest is not null && strongest.LanguageSkillName != weakest.LanguageSkillName
                          ? $"Kỹ năng tốt nhất hiện tại: {strongest.LanguageSkillName} ({strongest.CurrentScorePercent:0.##}%). Cần ưu tiên: {weakest.LanguageSkillName} ({weakest.CurrentScorePercent:0.##}%)."
                          : $"Kỹ năng nổi bật hiện tại: {strongest?.LanguageSkillName ?? "chưa có dữ liệu"}.")
                    : "Chưa có bài Placement Test hoặc Mock Test đã nộp trong khoảng thời gian này.";

                return ApiResponse<SkillMonthlyAnalyticsResponse>.Success(new SkillMonthlyAnalyticsResponse
                {
                    MonthsRequested = months,
                    AssessedJlptLevel = user?.AssessedCertificateLevel?.Code,
                    TargetJlptLevel = user?.TargetCertificateLevel?.Code,
                    TotalAttempts = rows.Select(row => row.MockTestAttemptId).Distinct().Count(),
                    MockTestAttempts = rows.Where(row => row.MockTestAttempt?.AttemptKind == RikiPath.Domain.Enums.AttemptKind.MockTest)
                        .Select(row => row.MockTestAttemptId).Distinct().Count(),
                    PlacementAttempts = rows.Where(row => row.MockTestAttempt?.AttemptKind == RikiPath.Domain.Enums.AttemptKind.Placement)
                        .Select(row => row.MockTestAttemptId).Distinct().Count(),
                    CurrentOverallScorePercent = latestOverallScore,
                    Summary = summary,
                    Skills = skillItems,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<SkillMonthlyAnalyticsResponse>.Fail(
                    "Không thể tải tiến trình năng lực theo kỹ năng.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private static string DescribeSkillScore(double score) => score switch
        {
            < 30 => "Cần xây lại nền tảng",
            < 50 => "Cần củng cố nhiều",
            < 70 => "Đang phát triển",
            < 85 => "Khá vững",
            _ => "Vững",
        };

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
