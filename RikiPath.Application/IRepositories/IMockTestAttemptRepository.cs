using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestAttemptRepository : IGenericRepository<MockTestAttempt>
    {
        Task<List<MockTestAttempt>> GetByUserAsync(int userId);
        Task<List<MockTestAttempt>> GetPlacementByUserAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<MockTestAttempt?> GetActivePlacementByUserAsync(
            int userId,
            DateTime serverNowUtc,
            CancellationToken cancellationToken = default);
        Task<MockTestAttempt?> GetLatestCompletedPlacementByUserAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<MockTestAttempt?> GetPlacementByIdAsync(
            int attemptId,
            CancellationToken cancellationToken = default);
        Task<MockTestAttempt?> GetPlacementForSubmissionAsync(
            int attemptId,
            CancellationToken cancellationToken = default);
        Task<MockTestAttempt?> GetWithBreakdownAsync(int attemptId);
        Task<MockTestAttempt?> GetForSubmissionAsync(
            int attemptId,
            CancellationToken cancellationToken = default);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
