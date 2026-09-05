using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;

namespace RikiPath.Application.IServices
{
    public interface IReviewService
    {
        Task<ApiResponse<DailyReviewQueueResponse>> GetDailyReviewQueueAsync(int userId, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitReviewResultResponse>> SubmitReviewResultAsync(
            int userId, SubmitReviewRequest request, CancellationToken cancellationToken);
    }
}
