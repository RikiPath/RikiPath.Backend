using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockQuestionRepository : GenericRepository<MockQuestion>, IMockQuestionRepository
    {
        public MockQuestionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockQuestion>> GetBySectionIdAsync(int practiceTestSectionId)
            => await _context.MockQuestions
                .Include(x => x.Options)
                .Where(x => x.MockTestSectionId == practiceTestSectionId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        public async Task<List<MockQuestion>> GetByIdsWithOptionsAsync(List<int> questionIds)
        {
            return await _context.MockQuestions
                .Include(q => q.Options)
                .Include(q => q.MockTestSection)
                    .ThenInclude(s => s.LanguageSkill)
                .Where(q => questionIds.Contains(q.Id))
                .ToListAsync();
        }
    }
}
