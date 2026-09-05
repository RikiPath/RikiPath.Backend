using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class VocabularyListRepository : GenericRepository<VocabularyList>, IVocabularyListRepository
    {
        public VocabularyListRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<VocabularyList>> GetByUserIdAsync(int userId)
            => await _context.VocabularyLists
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Name)
                .ToListAsync();

        public async Task<VocabularyList?> GetWithEntriesAsync(int vocabularyListId)
            => await _context.VocabularyLists
                .Include(x => x.VocabularyNoteEntries)
                .FirstOrDefaultAsync(x => x.Id == vocabularyListId);
    }
}
