using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IRecommendedLearningPathRepository : IGenericRepository<RecommendedLearningPath>
    {
        Task<RecommendedLearningPath?> GetLatestByUserIdAsync(int userId);
        Task<List<RecommendedLearningPath>> GetByUserIdAsync(int userId);
    }
}
