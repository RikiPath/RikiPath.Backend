using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public class LearnerEssayRepository(AppDbContext context)
    : GenericRepository<LearnerEssay>(context), ILearnerEssayRepository
{
    public async Task<List<LearnerEssay>> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => await _context.LearnerEssays
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken);
}
