using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LearningPathSuggestionRepository : GenericRepository<LearningPathSuggestion>, ILearningPathSuggestionRepository
    {
        public LearningPathSuggestionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<LearningPathSuggestion?> GetLatestByUserIdAsync(int userId)
            => await _context.LearningPathSuggestions
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.GeneratedAt)
                .FirstOrDefaultAsync();

        public async Task<List<LearningPathSuggestion>> GetByUserIdAsync(int userId)
            => await _context.LearningPathSuggestions
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.GeneratedAt)
                .ToListAsync();
    }
}
