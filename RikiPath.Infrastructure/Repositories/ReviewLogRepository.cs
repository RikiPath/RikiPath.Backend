using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ReviewLogRepository : GenericRepository<ReviewLog>, IReviewLogRepository
    {
        public ReviewLogRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ReviewLog>> GetByReviewItemIdAsync(int reviewItemId)
            => await _context.ReviewLogs
                .Where(x => x.ReviewItemId == reviewItemId)
                .OrderByDescending(x => x.ReviewedAt)
                .ToListAsync();

        public async Task<List<DateTime>> GetActivityDatesAsync(int userId)
        {
            return await _context.ReviewLogs
                .Where(x => x.ReviewItem.UserId == userId)
                .Select(x => x.ReviewedAt.Date)
                .Distinct()
                .ToListAsync();
        }
    }
}
