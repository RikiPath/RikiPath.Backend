using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeQuestionRepository : GenericRepository<PracticeQuestion>, IPracticeQuestionRepository
    {
        public PracticeQuestionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeQuestion>> GetBySectionIdAsync(int practiceTestSectionId)
            => await _context.PracticeQuestions
                .Include(x => x.Options)
                .Where(x => x.PracticeTestSectionId == practiceTestSectionId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        public async Task<List<PracticeQuestion>> GetByIdsWithOptionsAsync(List<int> questionIds)
        {
            return await _context.PracticeQuestions
                .Include(q => q.Options)
                .Include(q => q.PracticeTestSection)
                    .ThenInclude(s => s.Skill)
                .Where(q => questionIds.Contains(q.Id))
                .ToListAsync();
        }
    }
}
