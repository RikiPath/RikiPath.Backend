using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILearnerNoteEntryRepository : IGenericRepository<LearnerNoteEntry>
    {
        /// <summary>Ownership is derived through LearnerNote (no direct UserId on this entity).</summary>
        Task<List<LearnerNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId, CancellationToken cancellationToken = default);
        Task<List<LearnerNoteEntry>> GetByPageIdAsync(int userId, int notebookPageId, CancellationToken cancellationToken = default);
        Task<(List<LearnerNoteEntry> Items, int TotalCount)> QuerySavedItemsAsync(
            int userId,
            string? likePattern,
            string? kind,
            int? pageId,
            int? fileId,
            int? folderId,
            bool? favoriteOnly,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<LearnerNoteEntry?> GetDetailedByIdAsync(int entryId, CancellationToken cancellationToken = default);
        Task<LearnerNoteEntry?> GetByIdForUpdateAsync(int entryId, CancellationToken cancellationToken = default);
    }
}
