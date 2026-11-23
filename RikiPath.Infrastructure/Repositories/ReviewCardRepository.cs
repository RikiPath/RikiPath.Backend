using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class ReviewCardRepository : GenericRepository<ReviewCard>, IReviewCardRepository
    {
        public ReviewCardRepository(AppDbContext context) : base(context)
        {
        }

        private IQueryable<ReviewCard> DetailedCardsQuery()
            => _context.ReviewCards
                .Include(x => x.LearnerNoteEntry)
                    .ThenInclude(x => x!.Vocabulary)
                .Include(x => x.LearnerNoteEntry)
                    .ThenInclude(x => x!.Kanji)
                .Include(x => x.LearnerNoteEntry)
                    .ThenInclude(x => x!.LearnerNote)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Include(x => x.Lesson)
                .Include(x => x.SrsCardTemplate);

        public async Task<ReviewCard?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId)
            => await _context.ReviewCards
                .FirstOrDefaultAsync(x => x.LearnerNoteEntryId == vocabularyNoteEntryId);

        public async Task<ReviewCard?> GetByIdForUpdateAsync(
            int cardId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.ReviewCards
                    .FromSqlInterpolated($"SELECT * FROM \"ReviewCards\" WHERE \"Id\" = {cardId} AND \"UserId\" = {userId} AND NOT \"IsDeleted\" FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await _context.ReviewCards
                    .FirstOrDefaultAsync(x => x.Id == cardId && x.UserId == userId && !x.IsDeleted, cancellationToken);
            }
        }

        public async Task<ReviewCard?> GetByIdDetailedAsync(
            int cardId,
            int userId,
            CancellationToken cancellationToken = default)
            => await DetailedCardsQuery()
                .FirstOrDefaultAsync(x => x.Id == cardId && x.UserId == userId, cancellationToken);

        public async Task<ReviewCard?> FindExistingCardAsync(
            int userId,
            int? vocabularyId,
            int? kanjiId,
            int? grammarPatternId,
            int? noteEntryId,
            int? srsCardTemplateId,
            string? normalizedContentKey,
            ReviewMode mode,
            CancellationToken cancellationToken = default)
        {
            var baseQuery = DetailedCardsQuery()
                .Where(x => x.UserId == userId && x.Mode == mode);

            if (vocabularyId.HasValue)
            {
                var found = await baseQuery.FirstOrDefaultAsync(x => x.VocabularyId == vocabularyId.Value, cancellationToken);
                if (found != null) return found;
            }

            if (kanjiId.HasValue)
            {
                var found = await baseQuery.FirstOrDefaultAsync(x => x.KanjiId == kanjiId.Value, cancellationToken);
                if (found != null) return found;
            }

            if (grammarPatternId.HasValue)
            {
                var found = await baseQuery.FirstOrDefaultAsync(x => x.GrammarPatternId == grammarPatternId.Value, cancellationToken);
                if (found != null) return found;
            }

            if (noteEntryId.HasValue)
            {
                var found = await baseQuery.FirstOrDefaultAsync(x => x.LearnerNoteEntryId == noteEntryId.Value, cancellationToken);
                if (found != null) return found;
            }

            if (srsCardTemplateId.HasValue)
            {
                var found = await baseQuery.FirstOrDefaultAsync(x => x.SrsCardTemplateId == srsCardTemplateId.Value, cancellationToken);
                if (found != null) return found;
            }

            if (!string.IsNullOrWhiteSpace(normalizedContentKey))
            {
                var key = normalizedContentKey.Trim();
                var found = await baseQuery.FirstOrDefaultAsync(
                    x => !x.IsDeleted && x.NormalizedContentKey == key,
                    cancellationToken);
                if (found != null) return found;

                return await baseQuery.FirstOrDefaultAsync(
                    x => x.NormalizedContentKey == key,
                    cancellationToken);
            }

            return null;
        }

        public async Task<List<ReviewCard>> GetAllForUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => await DetailedCardsQuery()
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Mode == ReviewMode.Recognition)
                .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<ReviewCard>> GetDueQueueAsync(
            int userId,
            DateTime asOfUtc,
            string? cardType = null,
            string? jlptLevel = null,
            string? sourceType = null,
            CancellationToken cancellationToken = default)
        {
            var query = DetailedCardsQuery()
                .Where(x => x.UserId == userId
                    && x.Mode == ReviewMode.Recognition
                    && !x.IsDeleted
                    && x.Status == SrsCardStatus.Active
                    && x.NextReviewDate <= asOfUtc);

            if (!string.IsNullOrWhiteSpace(cardType))
            {
                var normalizedTypeLower = cardType.Trim().ToLowerInvariant();
                query = query.Where(x => x.CardType.ToLower() == normalizedTypeLower);
            }

            if (!string.IsNullOrWhiteSpace(jlptLevel))
            {
                var level = jlptLevel.Trim().ToUpperInvariant();
                query = query.Where(x => x.JlptLevel == level);
            }

            if (!string.IsNullOrWhiteSpace(sourceType))
            {
                var source = sourceType.Trim();
                query = query.Where(x => x.SourceType == source);
            }

            return await query
                .OrderBy(x => x.LearningState == SrsLearningState.Relearning ? 0 : x.LearningState == SrsLearningState.Learning ? 1 : x.LearningState == SrsLearningState.New ? 2 : 3)
                .ThenBy(x => x.NextReviewDate)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasReviewHistoryIdempotencyAsync(
            int reviewCardId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => await _context.ReviewHistories.AnyAsync(
                x => x.ReviewCardId == reviewCardId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }
}
