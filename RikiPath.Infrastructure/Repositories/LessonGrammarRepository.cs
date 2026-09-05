using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonGrammarRepository : GenericRepository<LessonGrammar>, ILessonGrammarRepository
    {
        public LessonGrammarRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LessonGrammar>> GetByLessonIdAsync(int lessonId)
            => await _context.LessonGrammars
                .Include(x => x.GrammarPoint)
                .Where(x => x.LessonId == lessonId)
                .ToListAsync();

        public async Task<bool> ExistsAsync(int lessonId, int grammarPointId)
            => await _context.LessonGrammars
                .AnyAsync(x => x.LessonId == lessonId && x.GrammarPointId == grammarPointId);
    }
}
