using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
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

        public async Task<List<MockTestSectionResult>> GetByAttemptIdAsync(
            int practiceTestAttemptId,
            CancellationToken cancellationToken = default)
            => await _context.MockTestSectionResults
                .Include(x => x.MockTestSection)
                    .ThenInclude(s => s.LanguageSkill)
                .Where(x => !x.IsDeleted && x.MockTestAttemptId == practiceTestAttemptId)
                .OrderBy(x => x.MockTestSection.SortOrder)
                .ToListAsync(cancellationToken);

        public async Task<List<MockTestSectionResult>> GetLanguageSkillBreakdownAsync(int userId)
        {
            return await _context.MockTestSectionResults
                .Include(x => x.MockTestSection)
                    .ThenInclude(s => s.LanguageSkill)
                .Where(x => !x.IsDeleted &&
                            !x.MockTestAttempt.IsDeleted &&
                            x.MockTestAttempt.UserId == userId &&
                            x.MockTestAttempt.IsCompleted &&
                            x.MockTestAttempt.AttemptKind == AttemptKind.MockTest)
                .ToListAsync();
        }
    }
}
