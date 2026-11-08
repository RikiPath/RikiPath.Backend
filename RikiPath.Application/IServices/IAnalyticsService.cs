using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Analytics;

namespace RikiPath.Application.IServices
{
    public interface IAnalyticsService
    {
        /// <summary>Chuỗi ngày học liên tục, tính từ hoạt động học (hoàn thành bài học, ôn SRS, làm bài thi thử).</summary>
        Task<ApiResponse<StudyStreakResponse>> GetStudyStreakAsync(CancellationToken cancellationToken);

        Task<ApiResponse<CompletionStatsResponse>> GetCompletionStatsAsync(CancellationToken cancellationToken);

        /// <summary>Điểm trung bình theo từng kỹ năng (Từ vựng/Kanji, Đọc hiểu, Nghe) từ các bài thi thử.</summary>
        Task<ApiResponse<List<LanguageSkillBreakdownItem>>> GetLanguageSkillBreakdownAsync(CancellationToken cancellationToken);

        /// <summary>Monthly per-skill performance from completed Mock Tests and Placement Tests.</summary>
        Task<ApiResponse<SkillMonthlyAnalyticsResponse>> GetMonthlySkillAnalyticsAsync(
            int months,
            CancellationToken cancellationToken);
    }
}
