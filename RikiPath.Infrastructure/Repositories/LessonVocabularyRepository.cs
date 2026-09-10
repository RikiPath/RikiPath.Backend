using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonVocabularyRepository : GenericRepository<LessonVocabulary>, ILessonVocabularyRepository
    {
        public LessonVocabularyRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LessonVocabulary>> GetByLessonIdAsync(int lessonId)
            => await _context.LessonVocabularies
                .Include(x => x.VocabularyEntry)
                .Where(x => x.LessonId == lessonId)
                .ToListAsync();

        public async Task<bool> ExistsAsync(int lessonId, int vocabularyEntryId)
            => await _context.LessonVocabularies
                .AnyAsync(x => x.LessonId == lessonId && x.VocabularyEntryId == vocabularyEntryId);
    }
}
