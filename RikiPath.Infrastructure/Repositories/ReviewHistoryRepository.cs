using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ReviewHistoryRepository : GenericRepository<ReviewHistory>, IReviewHistoryRepository
    {
        public ReviewHistoryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ReviewHistory>> GetByReviewCardIdAsync(int reviewItemId)
            => await _context.ReviewHistories
                .Where(x => x.ReviewCardId == reviewItemId)
                .OrderByDescending(x => x.ReviewedAt)
                .ToListAsync();

        public async Task<List<DateTime>> GetActivityDatesAsync(int userId)
        {
            return await _context.ReviewHistories
                .Where(x => x.ReviewCard.UserId == userId)
                .Select(x => x.ReviewedAt.Date)
                .Distinct()
                .ToListAsync();
        }
    }
}
