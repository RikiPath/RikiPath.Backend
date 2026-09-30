using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LearnerNoteEntryRepository : GenericRepository<LearnerNoteEntry>, ILearnerNoteEntryRepository
    {
        public LearnerNoteEntryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LearnerNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId)
        {
            IQueryable<LearnerNoteEntry> query = _context.LearnerNoteEntries
                .Include(x => x.LearnerNote)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Where(x => x.LearnerNote.UserId == userId);

            if (vocabularyListId.HasValue)
                query = query.Where(x => x.LearnerNoteId == vocabularyListId.Value);

            return await query.OrderByDescending(x => x.CreatedDate).ToListAsync();
        }
    }
}
