using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface ISrsReviewSessionRepository : IGenericRepository<SrsReviewSession>
{
    Task<SrsReviewSession?> GetActiveByUserIdAsync(int userId, string? sessionKind = null, CancellationToken cancellationToken = default);
    Task<SrsReviewSession?> GetBySessionKeyForUpdateAsync(Guid sessionKey, int userId, CancellationToken cancellationToken = default);
}
