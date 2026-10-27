using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PlacementTests;

namespace RikiPath.Application.IServices
{
    public interface IPlacementTestService
    {
        Task<ApiResponse<PlacementTestStatusResponse>> GetStatusAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestStatusResponse>> SkipPlacementAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse?>> GetActiveAttemptAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> SubmitPlacementTestAsync(
            int attemptId,
            SubmitPlacementTestRequest request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> GetPlacementTestResultAsync(
            int attemptId,
            CancellationToken cancellationToken = default);
    }
}
