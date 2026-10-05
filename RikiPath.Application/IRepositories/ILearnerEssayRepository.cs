using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface ILearnerEssayRepository : IGenericRepository<LearnerEssay>
{
    Task<List<LearnerEssay>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
