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
                    .ThenInclude(x => x.Vocabulary)
                .Include(x => x.LearnerNoteEntry)
                    .ThenInclude(x => x.Kanji)
                .Include(x => x.LearnerNoteEntry)
                    .ThenInclude(x => x.LearnerNote)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Where(x => x.UserId == userId
                    && x.Mode == RikiPath.Domain.Enums.ReviewMode.Recognition
                    && !x.IsDeleted
                    && x.NextReviewDate <= asOf)
                .Where(x =>
                    (x.LearnerNoteEntryId != null
                        && x.LearnerNoteEntry != null
                        && !x.LearnerNoteEntry.IsDeleted
                        && !x.LearnerNoteEntry.LearnerNote.IsDeleted
                        && (x.LearnerNoteEntry.VocabularyId == null
                            || (x.LearnerNoteEntry.Vocabulary != null && !x.LearnerNoteEntry.Vocabulary.IsDeleted))
                        && (x.LearnerNoteEntry.KanjiId == null
                            || (x.LearnerNoteEntry.Kanji != null && !x.LearnerNoteEntry.Kanji.IsDeleted)))
                    || (x.KanjiId != null && x.Kanji != null && !x.Kanji.IsDeleted)
                    || (x.GrammarPatternId != null && x.GrammarPattern != null && !x.GrammarPattern.IsDeleted))
                .OrderBy(x => x.NextReviewDate)
                .ToListAsync();
        public async Task<ReviewCard?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId)
        {
            return await _context.ReviewCards
                .FirstOrDefaultAsync(x => x.LearnerNoteEntryId == vocabularyNoteEntryId);
        }
    }
}
