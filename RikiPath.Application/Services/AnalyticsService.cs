using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Analytics;
using System.Net;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    // NOTE: cần các hàm repo sau (không cần entity mới, chỉ query lại dữ liệu đã có):
    //   ILessonProgressRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ CompletedAt
    //   IReviewHistoryRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ ReviewedAt
    //     (cần join qua ReviewCard để lọc theo userId)
    //   IMockTestAttemptRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ SubmittedAt
    //   ILessonRepository.GetCompletionStatsAsync(userId) -> (int TotalLessons, int CompletedLessons),
    //     lọc theo Course.JlptLevelId == user.TargetJlptLevelId nếu user đã set mục tiêu
    //   IMockTestSectionResultRepository.GetLanguageSkillBreakdownAsync(userId)
    //     -> List<(string LanguageSkillName, double AverageScore, int AttemptCount)>, GROUP BY LanguageSkill.Name
    public class AnalyticsService(IUnitOfWork unitOfWork, IClaimService claimService) : IAnalyticsService
    {
        public async Task<ApiResponse<StudyStreakResponse>> GetStudyStreakAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lessonDates = await unitOfWork.LessonProgresses.GetActivityDatesAsync(userId);
                var reviewDates = await unitOfWork.ReviewHistories.GetActivityDatesAsync(userId);
                var testDates = await unitOfWork.MockTestAttempts.GetActivityDatesAsync(userId);

                var allDates = lessonDates.Concat(reviewDates).Concat(testDates)
                    .Select(d => d.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                if (allDates.Count == 0)
                {
                    return ApiResponse<StudyStreakResponse>.Success(new StudyStreakResponse
                    {
                        CurrentStreakDays = 0,
                        LongestStreakDays = 0,
                        LastActivityDate = null,
                        IsActiveToday = false,
                    });
                }

                var today = DateTime.UtcNow.Date;
                var lastActivity = allDates[0];

                // Current streak: đếm ngược từ hôm nay (hoặc hôm qua nếu hôm nay chưa học) miễn
                // là không có ngày nào bị đứt quãng.
                var currentStreak = 0;
                var cursor = lastActivity == today ? today : (lastActivity == today.AddDays(-1) ? today.AddDays(-1) : (DateTime?)null);
                if (cursor is not null)
                {
                    var dateSet = allDates.ToHashSet();
                    while (dateSet.Contains(cursor.Value))
                    {
                        currentStreak++;
                        cursor = cursor.Value.AddDays(-1);
                    }
                }

                // Longest streak: quét toàn bộ danh sách ngày (đã sort giảm dần) tìm dải liên tiếp dài nhất.
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

                return ApiResponse<StudyStreakResponse>.Success(new StudyStreakResponse
                {
                    CurrentStreakDays = currentStreak,
                    LongestStreakDays = Math.Max(longestStreak, currentStreak),
                    LastActivityDate = lastActivity,
                    IsActiveToday = lastActivity == today,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<StudyStreakResponse>.Fail(
                    "Không thể tính chuỗi ngày học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
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
