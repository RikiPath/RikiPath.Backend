using RikiPath.Application.Requests.LearningMoments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningMoments;

namespace RikiPath.Application.IServices
{
    /// <summary>
    /// Learning Moments: celebration-worthy progress derived from verified learning data, shown at most once
    /// per learner + occurrence (atomic claim across tabs/devices), plus the learner's achievement history.
    /// </summary>
    public interface ILearningMomentService
    {
        Task<ApiResponse<PendingLearningMomentsResponse>> GetPendingAsync(CancellationToken cancellationToken);

        Task<ApiResponse<ClaimLearningMomentResponse>> ClaimAsync(ClaimLearningMomentRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<LearnerAchievementsResponse>> GetAchievementsAsync(CancellationToken cancellationToken);
    }
}
