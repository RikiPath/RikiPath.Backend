using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.Application.IServices
{
    public interface ILearningPathService
    {
        Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(
             GenerateLearningPathRequest request,
             CancellationToken cancellationToken);

        Task<ApiResponse<LearningPathResponse?>> GetLatestLearningPathAsync(
             CancellationToken cancellationToken);

        Task<ApiResponse<LearningPathResponse>> PreviewLearningPathAsync(
            PreviewLearningPathRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<LearningPathResponse>> ConfirmPreviewAsync(int previewId, CancellationToken cancellationToken);
        Task<ApiResponse<LearningPathResponse>> CancelPreviewAsync(int previewId, CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<LearningPathResponse>>> GetHistoryAsync(CancellationToken cancellationToken);
    }
}
