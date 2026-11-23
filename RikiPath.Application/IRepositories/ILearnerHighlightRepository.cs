using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface ILearnerHighlightRepository : IGenericRepository<LearnerHighlight>
{
    Task<List<LearnerHighlight>> GetByUserAndTargetAsync(
        int userId,
        string? targetType,
        int? targetId,
        string? targetKey,
        CancellationToken cancellationToken = default);

    Task<LearnerHighlight?> GetByIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);
}
