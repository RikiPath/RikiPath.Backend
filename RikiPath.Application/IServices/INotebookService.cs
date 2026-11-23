using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Application.IServices
{
    public interface INotebookService
    {
        // V3 Hierarchy & Tree
        Task<ApiResponse<NotebookTreeResponse>> GetTreeAsync(CancellationToken cancellationToken);

        // Folders (Level 1)
        Task<ApiResponse<List<NotebookFolderTreeResponse>>> GetFoldersAsync(CancellationToken cancellationToken);
        Task<ApiResponse<NotebookFolderTreeResponse>> CreateFolderAsync(CreateNotebookFolderRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookFolderTreeResponse>> UpdateFolderAsync(int folderId, UpdateNotebookFolderRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> DeleteFolderAsync(int folderId, bool cascade, CancellationToken cancellationToken);

        // Notebook Files (Level 2 — stored in "LearnerNotes")
        Task<ApiResponse<NotebookFileTreeResponse>> GetFileAsync(int fileId, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookFileTreeResponse>> CreateFileAsync(CreateNotebookFileRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookFileTreeResponse>> UpdateFileAsync(int fileId, UpdateNotebookFileRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> DeleteFileAsync(int fileId, bool cascade, CancellationToken cancellationToken);

        // Notebook Pages (Level 3 — stored in "NotebookPages")
        Task<ApiResponse<NotebookPageDetailResponse>> GetPageAsync(int pageId, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookPageDetailResponse>> CreatePageAsync(CreateNotebookPageRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookPageDetailResponse>> UpdatePageAsync(int pageId, UpdateNotebookPageRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookPageSummaryResponse>> TogglePageFavoriteAsync(int pageId, bool isFavorite, CancellationToken cancellationToken);
        Task<ApiResponse> DeletePageAsync(int pageId, CancellationToken cancellationToken);

        // Smart Views & Server Search
        Task<ApiResponse<List<NotebookPageSummaryResponse>>> GetFavoritePagesAsync(CancellationToken cancellationToken);
        Task<ApiResponse<NotebookSavedItemsPageResponse>> GetSavedItemsAsync(
            string? search,
            string? kind,
            int? pageId,
            int? fileId,
            int? folderId,
            bool? favoriteOnly,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken);
        Task<ApiResponse<NotebookSearchResponse>> SearchAsync(string? query, int limit, CancellationToken cancellationToken);

        // Quick Save from Smart Text Selection (idempotent default Folder -> File -> Page, NO forced SRS/Highlight)
        Task<ApiResponse<NotebookEntryResponse>> QuickSaveAsync(QuickSaveNotebookRequest request, CancellationToken cancellationToken);

        // Legacy + Shared Compatibility Endpoints
        Task<ApiResponse<LearnerNoteResponse>> CreateListAsync(CreateLearnerNoteRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<List<LearnerNoteResponse>>> GetMyListsAsync(CancellationToken cancellationToken);
        Task<ApiResponse<LearnerNoteResponse>> UpdateListAsync(int listId, UpdateLearnerNoteRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> DeleteListAsync(int listId, CancellationToken cancellationToken);
        Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(int listId, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(BookmarkVocabularyRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(BookmarkKanjiRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(int grammarPointId, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(AddManualEntryRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookEntryResponse>> UpdateEntryAsync(int noteEntryId, UpdateNotebookEntryRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<NotebookEntryResponse>> EnrollEntryToSrsAsync(int noteEntryId, CancellationToken cancellationToken);
        Task<ApiResponse> RemoveEntryAsync(int noteEntryId, CancellationToken cancellationToken);
    }
}
