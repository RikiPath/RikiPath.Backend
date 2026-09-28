using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestSectionRepository : GenericRepository<MockTestSection>, IMockTestSectionRepository
    {
        public MockTestSectionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestSection>> GetByMockTestIdAsync(int practiceTestId)
            => await _context.MockTestSections
                .Include(x => x.LanguageSkill)
                .Where(x => x.MockTestId == practiceTestId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
    }
}
