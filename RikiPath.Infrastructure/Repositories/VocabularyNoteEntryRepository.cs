using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class VocabularyNoteEntryRepository : GenericRepository<VocabularyNoteEntry>, IVocabularyNoteEntryRepository
    {
        public VocabularyNoteEntryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<VocabularyNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId)
        {
            IQueryable<VocabularyNoteEntry> query = _context.VocabularyNoteEntries
                .Include(x => x.VocabularyList)
                .Include(x => x.VocabularyEntry)
                .Include(x => x.KanjiEntry)
                .Where(x => x.VocabularyList.UserId == userId);

            if (vocabularyListId.HasValue)
                query = query.Where(x => x.VocabularyListId == vocabularyListId.Value);

            return await query.OrderByDescending(x => x.CreatedDate).ToListAsync();
        }
    }
}
