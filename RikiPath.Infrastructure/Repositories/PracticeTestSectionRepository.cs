using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeTestSectionRepository : GenericRepository<PracticeTestSection>, IPracticeTestSectionRepository
    {
        public PracticeTestSectionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeTestSection>> GetByPracticeTestIdAsync(int practiceTestId)
            => await _context.PracticeTestSections
                .Include(x => x.Skill)
                .Where(x => x.PracticeTestId == practiceTestId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
    }
}
