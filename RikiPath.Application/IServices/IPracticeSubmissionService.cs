using RikiPath.Application.Common;
using RikiPath.Application.Requests.Practice;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Practice;

namespace RikiPath.Application.IServices
{
    public interface IPracticeSubmissionService
    {
        Task<ApiResponse<PracticeSubmissionResponse>> SubmitAsync(
            int userId, SubmitPracticeRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<PracticeSubmissionResponse>> GetByIdAsync(
            int userId, int submissionId, CancellationToken cancellationToken);

        Task<ApiResponse<List<PracticeSubmissionResponse>>> GetMyHistoryAsync(
            int userId, CancellationToken cancellationToken);

        // Cho phép chấm lại nếu lần chấm trước lỗi (AI timeout, v.v.)
        Task<ApiResponse<PracticeSubmissionResponse>> RegradeAsync(
            int userId, int submissionId, CancellationToken cancellationToken);
    }
}
