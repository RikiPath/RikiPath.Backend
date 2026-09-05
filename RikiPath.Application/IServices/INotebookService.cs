using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;

namespace RikiPath.Application.IServices
{
    public interface INotebookService
    {
        Task<ApiResponse<VocabularyListResponse>> CreateListAsync(
            int userId, CreateVocabularyListRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<VocabularyListResponse>>> GetMyListsAsync(int userId, CancellationToken cancellationToken);

        Task<ApiResponse<VocabularyListResponse>> UpdateListAsync(
            int userId, int listId, UpdateVocabularyListRequest request, CancellationToken cancellationToken);

        Task<ApiResponse> DeleteListAsync(int userId, int listId, CancellationToken cancellationToken);

        Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(
            int userId, int listId, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 từ vựng từ ngân hàng chung vào sổ tay + tự thêm vào hàng chờ SRS.</summary>
        Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(
            int userId, BookmarkVocabularyRequest request, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 Kanji từ ngân hàng chung vào sổ tay + tự thêm vào hàng chờ SRS.</summary>
        Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(
            int userId, BookmarkKanjiRequest request, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 điểm ngữ pháp thẳng vào hàng chờ SRS (không qua VocabularyList).</summary>
        Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(
            int userId, int grammarPointId, CancellationToken cancellationToken);

        Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(
            int userId, AddManualEntryRequest request, CancellationToken cancellationToken);

        Task<ApiResponse> RemoveEntryAsync(int userId, int noteEntryId, CancellationToken cancellationToken);
    }
}
