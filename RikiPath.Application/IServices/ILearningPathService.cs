using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.Application.IServices
{
    public interface ILearningPathService
    {
        Task<ApiResponse<LearningPathResponse>> GenerateLearningPathAsync(int userId, CancellationToken cancellationToken);
    }
}
