using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public class SrsReviewSessionRepository(AppDbContext context)
    : GenericRepository<SrsReviewSession>(context), ISrsReviewSessionRepository
{
    public async Task<SrsReviewSession?> GetActiveByUserIdAsync(
        int userId,
        string? sessionKind = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SrsReviewSessions
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Status == "Active");
        if (!string.IsNullOrWhiteSpace(sessionKind))
        {
            var normalizedKind = sessionKind.Trim();
            query = query.Where(x => x.SessionKind == normalizedKind);
        }

        return await query
            .OrderByDescending(x => x.LastActivityAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SrsReviewSession?> GetBySessionKeyForUpdateAsync(
        Guid sessionKey,
        int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SrsReviewSessions
                .FromSqlInterpolated($"SELECT * FROM \"SrsReviewSessions\" WHERE \"SessionKey\" = {sessionKey} AND \"UserId\" = {userId} AND NOT \"IsDeleted\" FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await _context.SrsReviewSessions
                .FirstOrDefaultAsync(x => x.SessionKey == sessionKey && x.UserId == userId && !x.IsDeleted, cancellationToken);
        }
    }
}
