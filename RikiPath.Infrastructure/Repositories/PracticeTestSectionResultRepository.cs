using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeTestSectionResultRepository : GenericRepository<PracticeTestSectionResult>, IPracticeTestSectionResultRepository
    {
        public PracticeTestSectionResultRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeTestSectionResult>> GetByAttemptIdAsync(int practiceTestAttemptId)
            => await _context.PracticeTestSectionResults
                .Include(x => x.PracticeTestSection)
                .Where(x => x.PracticeTestAttemptId == practiceTestAttemptId)
                .ToListAsync();
        public async Task<List<PracticeTestSectionResult>> GetSkillBreakdownAsync(int userId)
        {
            return await _context.PracticeTestSectionResults
                .Include(x => x.PracticeTestSection)
                    .ThenInclude(s => s.Skill)
                .Where(x => x.PracticeTestAttempt.UserId == userId && x.PracticeTestAttempt.IsCompleted)
                .ToListAsync();
        }
    }
}
