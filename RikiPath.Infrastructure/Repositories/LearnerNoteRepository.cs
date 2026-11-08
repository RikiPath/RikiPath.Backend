using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure.Repositories
{
    public class LearnerNoteRepository : GenericRepository<LearnerNote>, ILearnerNoteRepository
    {
        public LearnerNoteRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LearnerNote>> GetByUserIdAsync(int userId)
            => await _context.LearnerNotes
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Name)
                .ToListAsync();

        public async Task<LearnerNote?> GetWithEntriesAsync(int vocabularyListId)
            => await _context.LearnerNotes
                .Include(x => x.LearnerNoteEntries)
                .FirstOrDefaultAsync(x => x.Id == vocabularyListId);
    }
}
