using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.Application.IServices;

public interface IStandardRoadmapService
{
    Task<ApiResponse<StandardRoadmapResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ApiResponse<StandardRoadmapResponse>> GetAssessmentRoadmapAsync(CancellationToken cancellationToken);
    Task<ApiResponse> CheckAssessmentLessonAsync(int lessonId, CancellationToken cancellationToken);
}
