using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class GradingResultRepository : GenericRepository<GradingResult>, IGradingResultRepository
    {
        public GradingResultRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<GradingResult?> GetBySubmissionIdAsync(int practiceSubmissionId)
            => await _context.GradingResults
                .FirstOrDefaultAsync(x => x.PracticeSubmissionId == practiceSubmissionId);
    }
}
