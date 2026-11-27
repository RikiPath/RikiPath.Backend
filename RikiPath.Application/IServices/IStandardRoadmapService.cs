using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.Profile;

namespace RikiPath.Application.IServices;

public interface IStandardRoadmapService
{
    Task<ApiResponse<StandardRoadmapResponse>> GetAsync(CancellationToken cancellationToken);
    Task<ApiResponse<StandardRoadmapResponse>> GetAssessmentRoadmapAsync(CancellationToken cancellationToken);
    Task<ApiResponse> CheckAssessmentLessonAsync(int lessonId, CancellationToken cancellationToken);
    Task<ApiResponse<LearnerSetupStateResponse>> ActivateAsync(CancellationToken cancellationToken);
}
