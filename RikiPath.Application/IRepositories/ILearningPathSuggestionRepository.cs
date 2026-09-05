using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILearningPathSuggestionRepository : IGenericRepository<LearningPathSuggestion>
    {
        Task<LearningPathSuggestion?> GetLatestByUserIdAsync(int userId);
        Task<List<LearningPathSuggestion>> GetByUserIdAsync(int userId);
    }
}
