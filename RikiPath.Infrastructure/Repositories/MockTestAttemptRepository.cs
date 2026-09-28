using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestAttemptRepository : GenericRepository<MockTestAttempt>, IMockTestAttemptRepository
    {
        public MockTestAttemptRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestAttempt>> GetByUserAsync(int userId)
            => await _context.MockTestAttempts
                .Include(x => x.MockTest)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync();

        public async Task<MockTestAttempt?> GetWithBreakdownAsync(int attemptId)
            => await _context.MockTestAttempts
                .Include(x => x.MockTest)
                .Include(x => x.Answers).ThenInclude(a => a.MockQuestion)
                .Include(x => x.SectionResults).ThenInclude(r => r.MockTestSection)
                .FirstOrDefaultAsync(x => x.Id == attemptId);
        public async Task<List<DateTime>> GetActivityDatesAsync(int userId)
        {
            return await _context.MockTestAttempts
                .Where(a => a.UserId == userId)
                .Select(a => a.StartedAt.Date)
                .Distinct()
                .ToListAsync();
        }
    }
}
