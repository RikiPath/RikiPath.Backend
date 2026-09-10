using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeQuestionOptionRepository : GenericRepository<PracticeQuestionOption>, IPracticeQuestionOptionRepository
    {
        public PracticeQuestionOptionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeQuestionOption>> GetByQuestionIdAsync(int practiceQuestionId)
            => await _context.PracticeQuestionOptions
                .Where(x => x.PracticeQuestionId == practiceQuestionId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
    }
}
