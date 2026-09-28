using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ReviewCardRepository : GenericRepository<ReviewCard>, IReviewCardRepository
    {
        public ReviewCardRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ReviewCard>> GetDueForReviewAsync(int userId, DateTime asOf)
            => await _context.ReviewCards
                .Include(x => x.LearnerNoteEntry)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Where(x => x.UserId == userId && x.NextReviewDate <= asOf)
                .OrderBy(x => x.NextReviewDate)
                .ToListAsync();
        public async Task<ReviewCard?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId)
        {
            return await _context.ReviewCards
                .FirstOrDefaultAsync(x => x.LearnerNoteEntryId == vocabularyNoteEntryId);
        }
    }
}
