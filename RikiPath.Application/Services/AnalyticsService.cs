using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Analytics;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần các hàm repo sau (không cần entity mới, chỉ query lại dữ liệu đã có):
    //   ILessonProgressRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ CompletedAt
    //   IReviewLogRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ ReviewedAt
    //     (cần join qua ReviewItem để lọc theo userId)
    //   IPracticeTestAttemptRepository.GetActivityDatesAsync(userId) -> distinct DateOnly từ SubmittedAt
    //   ILessonRepository.GetCompletionStatsAsync(userId) -> (int TotalLessons, int CompletedLessons),
    //     lọc theo Course.JlptLevelId == user.TargetJlptLevelId nếu user đã set mục tiêu
    //   IPracticeTestSectionResultRepository.GetSkillBreakdownAsync(userId)
    //     -> List<(string SkillName, double AverageScore, int AttemptCount)>, GROUP BY Skill.Name
    public class AnalyticsService(IUnitOfWork unitOfWork) : IAnalyticsService
    {
        public async Task<ApiResponse<StudyStreakResponse>> GetStudyStreakAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                var lessonDates = await unitOfWork.LessonProgresses.GetActivityDatesAsync(userId);
                var reviewDates = await unitOfWork.ReviewLogs.GetActivityDatesAsync(userId);
                var testDates = await unitOfWork.PracticeTestAttempts.GetActivityDatesAsync(userId);

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

        public async Task<ApiResponse<CompletionStatsResponse>> GetCompletionStatsAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
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

        public async Task<ApiResponse<List<SkillBreakdownItem>>> GetSkillBreakdownAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                var breakdown = await unitOfWork.PracticeTestSectionResults.GetSkillBreakdownAsync(userId);

                var result = breakdown
                 .Where(b => b.PracticeTestSection?.Skill != null)
                 .GroupBy(b => b.PracticeTestSection.Skill.Name)
                 .Select(g => new SkillBreakdownItem
                 {
                     SkillName = g.Key,
                     AverageScore = Math.Round(g.Average(x => x.ScorePercent), 2),
                     AttemptCount = g.Count(),
                 })
                 .ToList();

                return ApiResponse<List<SkillBreakdownItem>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<SkillBreakdownItem>>.Fail(
                    "Không thể tải phân tích điểm mạnh/yếu.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

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
