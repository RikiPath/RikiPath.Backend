using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonKanjiRepository : GenericRepository<LessonKanji>, ILessonKanjiRepository
    {
        public LessonKanjiRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LessonKanji>> GetByLessonIdAsync(int lessonId)
            => await _context.LessonKanjis
                .Include(x => x.KanjiEntry)
                .Where(x => x.LessonId == lessonId)
                .ToListAsync();

        public async Task<bool> ExistsAsync(int lessonId, int kanjiEntryId)
            => await _context.LessonKanjis
                .AnyAsync(x => x.LessonId == lessonId && x.KanjiEntryId == kanjiEntryId);
    }
}
