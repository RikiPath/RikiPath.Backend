using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories
{
    public interface IReviewCardRepository : IGenericRepository<ReviewCard>
    {
        Task<ReviewCard?> GetByNoteEntryIdAsync(int vocabularyNoteEntryId);

        Task<ReviewCard?> GetByIdForUpdateAsync(int cardId, int userId, CancellationToken cancellationToken = default);
        Task<ReviewCard?> GetByIdDetailedAsync(int cardId, int userId, CancellationToken cancellationToken = default);
        Task<ReviewCard?> FindExistingCardAsync(
            int userId,
            int? vocabularyId,
            int? kanjiId,
            int? grammarPatternId,
            int? noteEntryId,
            int? srsCardTemplateId,
            string? normalizedContentKey,
            ReviewMode mode,
            CancellationToken cancellationToken = default);
        Task<List<ReviewCard>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<ReviewCard>> GetDueQueueAsync(
            int userId,
            DateTime asOfUtc,
            string? cardType = null,
            string? jlptLevel = null,
            string? sourceType = null,
            CancellationToken cancellationToken = default);
        Task<bool> HasReviewHistoryIdempotencyAsync(
            int reviewCardId,
            string idempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
