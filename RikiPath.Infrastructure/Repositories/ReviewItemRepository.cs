using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ReviewItemRepository : GenericRepository<ReviewItem>, IReviewItemRepository
    {
        public ReviewItemRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ReviewItem>> GetDueForReviewAsync(int userId, DateTime asOf)
            => await _context.ReviewItems
                .Include(x => x.VocabularyNoteEntry)
                .Include(x => x.KanjiEntry)
                .Include(x => x.GrammarPoint)
                .Where(x => x.UserId == userId && x.NextReviewDate <= asOf)
                .OrderBy(x => x.NextReviewDate)
                .ToListAsync();
        public async Task<ReviewItem?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId)
        {
            return await _context.ReviewItems
                .FirstOrDefaultAsync(x => x.VocabularyNoteEntryId == vocabularyNoteEntryId);
        }
    }
}
