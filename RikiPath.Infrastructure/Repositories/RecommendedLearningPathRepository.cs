using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class RecommendedLearningPathRepository : GenericRepository<RecommendedLearningPath>, IRecommendedLearningPathRepository
    {
        public RecommendedLearningPathRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<RecommendedLearningPath?> GetLatestByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => await _context.RecommendedLearningPaths
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Status == LearningPathStatus.Active)
                .OrderByDescending(x => x.GeneratedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<RecommendedLearningPath?> GetActiveByUserIdAsync(
            int userId, CancellationToken cancellationToken = default)
            => await _context.RecommendedLearningPaths
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Status == LearningPathStatus.Active,
                    cancellationToken);

        public async Task<RecommendedLearningPath?> GetOwnedByIdAsync(
            int userId, int pathId, CancellationToken cancellationToken = default)
            => await _context.RecommendedLearningPaths
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == pathId, cancellationToken);

        public async Task<int> GetNextVersionAsync(int userId, CancellationToken cancellationToken = default)
            => (await _context.RecommendedLearningPaths
                .Where(x => x.UserId == userId)
                .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;

        public async Task<List<RecommendedLearningPath>> GetByUserIdAsync(int userId)
            => await _context.RecommendedLearningPaths
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.GeneratedAt)
                .ToListAsync();
    }
}
