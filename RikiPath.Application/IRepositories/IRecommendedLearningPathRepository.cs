using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IRecommendedLearningPathRepository : IGenericRepository<RecommendedLearningPath>
    {
        Task<RecommendedLearningPath?> GetLatestByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<List<RecommendedLearningPath>> GetByUserIdAsync(int userId);
        Task<RecommendedLearningPath?> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<RecommendedLearningPath?> GetOwnedByIdAsync(int userId, int pathId, CancellationToken cancellationToken = default);
        Task<int> GetNextVersionAsync(int userId, CancellationToken cancellationToken = default);
    }
}
