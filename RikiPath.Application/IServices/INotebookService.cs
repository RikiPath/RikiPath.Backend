using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;

namespace RikiPath.Application.IServices
{
    public interface INotebookService
    {
        Task<ApiResponse<VocabularyListResponse>> CreateListAsync(CreateVocabularyListRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<VocabularyListResponse>>> GetMyListsAsync(CancellationToken cancellationToken);

        Task<ApiResponse<VocabularyListResponse>> UpdateListAsync(
            int listId, UpdateVocabularyListRequest request, CancellationToken cancellationToken);

        Task<ApiResponse> DeleteListAsync(int listId, CancellationToken cancellationToken);

        Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(
            int listId, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 từ vựng từ ngân hàng chung vào sổ tay + tự thêm vào hàng chờ SRS.</summary>
        Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(BookmarkVocabularyRequest request, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 Kanji từ ngân hàng chung vào sổ tay + tự thêm vào hàng chờ SRS.</summary>
        Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(BookmarkKanjiRequest request, CancellationToken cancellationToken);

        /// <summary>Bookmark 1 điểm ngữ pháp thẳng vào hàng chờ SRS (không qua VocabularyList).</summary>
        Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(int grammarPointId, CancellationToken cancellationToken);

        Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(AddManualEntryRequest request, CancellationToken cancellationToken);

        Task<ApiResponse> RemoveEntryAsync(int noteEntryId, CancellationToken cancellationToken);
    }
}
