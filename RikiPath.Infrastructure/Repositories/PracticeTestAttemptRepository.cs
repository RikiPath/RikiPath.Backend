using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeTestAttemptRepository : GenericRepository<PracticeTestAttempt>, IPracticeTestAttemptRepository
    {
        public PracticeTestAttemptRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeTestAttempt>> GetByUserAsync(int userId)
            => await _context.PracticeTestAttempts
                .Include(x => x.PracticeTest)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync();

        public async Task<PracticeTestAttempt?> GetWithBreakdownAsync(int attemptId)
            => await _context.PracticeTestAttempts
                .Include(x => x.PracticeTest)
                .Include(x => x.Answers).ThenInclude(a => a.PracticeQuestion)
                .Include(x => x.SectionResults).ThenInclude(r => r.PracticeTestSection)
                .FirstOrDefaultAsync(x => x.Id == attemptId);
        public async Task<List<DateTime>> GetActivityDatesAsync(int userId)
        {
            return await _context.PracticeTestAttempts
                .Where(a => a.UserId == userId)
                .Select(a => a.StartedAt.Date)
                .Distinct()
                .ToListAsync();
        }
    }
}
