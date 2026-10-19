using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonProgressRepository : GenericRepository<LessonProgress>, ILessonProgressRepository
    {
        public LessonProgressRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<LessonProgress?> GetByUserAndLessonAsync(
            int userId,
            int lessonId,
            CancellationToken cancellationToken = default)
            => await _context.LessonProgresses
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.LessonId == lessonId,
                    cancellationToken);

        public async Task<List<LessonProgress>> GetByUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => await _context.LessonProgresses
                .Include(x => x.Lesson)
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

        public async Task<List<DateTime>> GetActivityDatesAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.LessonProgresses
                .Where(p => p.UserId == userId && p.LastWatchedAt.HasValue)
                .Select(p => p.LastWatchedAt!.Value.Date)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}
