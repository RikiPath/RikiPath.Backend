using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public class LearnerHighlightRepository(AppDbContext context)
    : GenericRepository<LearnerHighlight>(context), ILearnerHighlightRepository
{
    public async Task<List<LearnerHighlight>> GetByUserAndTargetAsync(
        int userId,
        string? targetType,
        int? targetId,
        string? targetKey,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LearnerHighlights
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            var normalizedType = targetType.Trim();
            query = query.Where(x => x.TargetType == normalizedType);
        }

        if (targetId.HasValue)
        {
            query = query.Where(x => x.TargetId == targetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(targetKey))
        {
            var normalizedKey = targetKey.Trim();
            query = query.Where(x => x.TargetKey == normalizedKey);
        }

        return await query
            .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<LearnerHighlight?> GetByIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default)
        => await _context.LearnerHighlights
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted, cancellationToken);
}
