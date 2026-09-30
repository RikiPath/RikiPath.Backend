using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class RecommendedLearningPathRepository : GenericRepository<RecommendedLearningPath>, IRecommendedLearningPathRepository
    {
        public RecommendedLearningPathRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<RecommendedLearningPath?> GetLatestByUserIdAsync(int userId)
            => await _context.RecommendedLearningPaths
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.GeneratedAt)
                .FirstOrDefaultAsync();

        public async Task<List<RecommendedLearningPath>> GetByUserIdAsync(int userId)
            => await _context.RecommendedLearningPaths
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.GeneratedAt)
                .ToListAsync();
    }
}
