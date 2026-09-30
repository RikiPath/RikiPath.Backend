using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestAnswerRepository : GenericRepository<MockTestAnswer>, IMockTestAnswerRepository
    {
        public MockTestAnswerRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestAnswer>> GetByAttemptIdAsync(int practiceTestAttemptId)
            => await _context.MockTestAnswers
                .Include(x => x.MockQuestion)
                .Include(x => x.SelectedOption)
                .Where(x => x.MockTestAttemptId == practiceTestAttemptId)
                .ToListAsync();
    }
}
