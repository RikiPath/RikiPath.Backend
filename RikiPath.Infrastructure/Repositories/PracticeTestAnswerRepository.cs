using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeTestAnswerRepository : GenericRepository<PracticeTestAnswer>, IPracticeTestAnswerRepository
    {
        public PracticeTestAnswerRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeTestAnswer>> GetByAttemptIdAsync(int practiceTestAttemptId)
            => await _context.PracticeTestAnswers
                .Include(x => x.PracticeQuestion)
                .Include(x => x.SelectedOption)
                .Where(x => x.PracticeTestAttemptId == practiceTestAttemptId)
                .ToListAsync();
    }
}
