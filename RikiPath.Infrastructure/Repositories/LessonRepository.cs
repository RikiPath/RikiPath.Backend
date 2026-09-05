using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonRepository : GenericRepository<Lesson>, ILessonRepository
    {
        public LessonRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<Lesson>> GetByCourseAsync(int courseId)
            => await _context.Lessons
                .Include(x => x.Skill)
                .Where(x => x.CourseId == courseId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

        public async Task<Lesson?> GetWithBankLinksAsync(int lessonId)
            => await _context.Lessons
                .Include(x => x.LessonKanjis).ThenInclude(x => x.KanjiEntry)
                .Include(x => x.LessonVocabularies).ThenInclude(x => x.VocabularyEntry)
                .Include(x => x.LessonGrammars).ThenInclude(x => x.GrammarPoint)
                .FirstOrDefaultAsync(x => x.Id == lessonId);

        public async Task<(int Total, int Completed)> GetCompletionStatsAsync(int userId)
        {
            var total = await _context.Lessons.CountAsync();
            var completed = await _context.LessonProgresses
                .CountAsync(x => x.UserId == userId && x.IsCompleted);

            return (total, completed);
        }
    }
}
