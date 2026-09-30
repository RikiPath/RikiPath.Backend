using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockQuestionOptionRepository : GenericRepository<MockQuestionOption>, IMockQuestionOptionRepository
    {
        public MockQuestionOptionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockQuestionOption>> GetByQuestionIdAsync(int practiceQuestionId)
            => await _context.MockQuestionOptions
                .Where(x => x.MockQuestionId == practiceQuestionId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
    }
}
