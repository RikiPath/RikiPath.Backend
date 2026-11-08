using RikiPath.Application.Requests.Practice;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Practice;

namespace RikiPath.Application.IServices
{
    /// <summary>Nộp bài luyện tập (bài luận/Kanji/Speaking) và chấm điểm bằng AI. Hiện chỉ Writing
    /// (bài luận) có pipeline chấm tự động; Kanji/Speaking được lưu lại, chờ bổ sung sau.</summary>
    public interface IPracticeSubmissionService
    {
        Task<ApiResponse<SubmitPracticeResponse>> SubmitAsync(
            SubmitPracticeRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<PracticeSubmissionDetailResponse>> GetByIdAsync(
            int submissionId, CancellationToken cancellationToken);

        Task<ApiResponse<List<PracticeSubmissionSummaryResponse>>> GetMyHistoryAsync(
            CancellationToken cancellationToken);

        /// <summary>Chấm lại một bài đã nộp (ghi đè GradingResult cũ nếu có).</summary>
        Task<ApiResponse<SubmitPracticeResponse>> RegradeAsync(
            int submissionId, CancellationToken cancellationToken);
    }
}