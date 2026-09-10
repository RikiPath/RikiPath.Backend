using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeSubmissionRepository : GenericRepository<PracticeSubmission>, IPracticeSubmissionRepository
    {
        public PracticeSubmissionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<PracticeSubmission>> GetByUserAsync(int userId, SubmissionType? type)
        {
            IQueryable<PracticeSubmission> query = _context.PracticeSubmissions.Where(x => x.UserId == userId);
            if (type.HasValue) query = query.Where(x => x.Type == type.Value);
            return await query.OrderByDescending(x => x.SubmittedAt).ToListAsync();
        }

        public async Task<PracticeSubmission?> GetWithGradingResultAsync(int id)
            => await _context.PracticeSubmissions
                .Include(x => x.GradingResult)
                .FirstOrDefaultAsync(x => x.Id == id);
    }
}
