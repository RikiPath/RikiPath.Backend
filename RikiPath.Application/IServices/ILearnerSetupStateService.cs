using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;

namespace RikiPath.Application.IServices;

/// <summary>
/// Centralized authoritative resolver for Learner onboarding, goal confirmation,
/// placement assessment state, standard/AI roadmap eligibility, and roadmap-scoped progress.
/// </summary>
public interface ILearnerSetupStateService
{
    Task<ApiResponse<LearnerSetupStateResponse>> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<LearnerSetupStateResponse?> ResolveForUserAsync(int userId, bool includeProgress = true, CancellationToken cancellationToken = default);
    Task<bool> HasValidCompletedPlacementForTargetAsync(int userId, int targetLevelId, CancellationToken cancellationToken = default);
}
