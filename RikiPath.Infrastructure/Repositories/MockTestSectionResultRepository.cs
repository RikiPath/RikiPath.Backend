using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestSectionResultRepository : GenericRepository<MockTestSectionResult>, IMockTestSectionResultRepository
    {
        public MockTestSectionResultRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestSectionResult>> GetByAttemptIdAsync(int practiceTestAttemptId)
            => await _context.MockTestSectionResults
                .Include(x => x.MockTestSection)
                .Where(x => x.MockTestAttemptId == practiceTestAttemptId)
                .ToListAsync();
        public async Task<List<MockTestSectionResult>> GetLanguageSkillBreakdownAsync(int userId)
        {
            return await _context.MockTestSectionResults
                .Include(x => x.MockTestSection)
                    .ThenInclude(s => s.LanguageSkill)
                .Where(x => x.MockTestAttempt.UserId == userId && x.MockTestAttempt.IsCompleted)
                .ToListAsync();
        }
    }
}
