using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    [RequireFeature(FeatureCodes.PersonalNotebook)]
    public class NotebookController(INotebookService notebookService) : ControllerBase
    {
        // =========================================================================
        // Personal Notebook V3: Hierarchy Tree, Folders, Files, Pages, Smart Views
        // =========================================================================

        /// <summary>Lấy toàn bộ cây điều hướng Sổ tay cá nhân (Folder -> File -> Page) kèm thống kê tổng quan.</summary>
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
        {
            var result = await notebookService.GetTreeAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy danh sách Thư mục của học viên hiện tại.</summary>
        [HttpGet("folders")]
        public async Task<IActionResult> GetFolders(CancellationToken cancellationToken)
        {
            var result = await notebookService.GetFoldersAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Tạo Thư mục ghi chú mới.</summary>
        [HttpPost("folders")]
        public async Task<IActionResult> CreateFolder([FromBody] CreateNotebookFolderRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.CreateFolderAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Đổi tên Thư mục ghi chú (có kiểm tra ExpectedRevision khi cung cấp).</summary>
        [HttpPut("folders/{folderId:int}")]
        public async Task<IActionResult> UpdateFolder(int folderId, [FromBody] UpdateNotebookFolderRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.UpdateFolderAsync(folderId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Xóa mềm Thư mục (yêu cầu cascade=true nếu bên trong còn tệp ghi chú; luôn giữ nguyên thẻ SRS).</summary>
        [HttpDelete("folders/{folderId:int}")]
        public async Task<IActionResult> DeleteFolder(int folderId, [FromQuery] bool cascade = false, CancellationToken cancellationToken = default)
        {
            var result = await notebookService.DeleteFolderAsync(folderId, cascade, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy chi tiết một Tệp ghi chú cùng danh sách các Trang bên trong.</summary>
        [HttpGet("files/{fileId:int}")]
        public async Task<IActionResult> GetFile(int fileId, CancellationToken cancellationToken)
        {
            var result = await notebookService.GetFileAsync(fileId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Tạo Tệp ghi chú mới bên trong một Thư mục (không yêu cầu upload tệp vật lý).</summary>
        [HttpPost("files")]
        public async Task<IActionResult> CreateFile([FromBody] CreateNotebookFileRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.CreateFileAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Đổi tên hoặc di chuyển Tệp ghi chú sang Thư mục khác.</summary>
        [HttpPut("files/{fileId:int}")]
        public async Task<IActionResult> UpdateFile(int fileId, [FromBody] UpdateNotebookFileRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.UpdateFileAsync(fileId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Xóa mềm Tệp ghi chú (yêu cầu cascade=true nếu còn trang; không xóa thẻ SRS).</summary>
        [HttpDelete("files/{fileId:int}")]
        public async Task<IActionResult> DeleteFile(int fileId, [FromQuery] bool cascade = false, CancellationToken cancellationToken = default)
        {
            var result = await notebookService.DeleteFileAsync(fileId, cascade, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy chi tiết Trang ghi chú (nội dung soạn thảo + mục đã lưu thuộc trang).</summary>
        [HttpGet("pages/{pageId:int}")]
        public async Task<IActionResult> GetPage(int pageId, CancellationToken cancellationToken)
        {
            var result = await notebookService.GetPageAsync(pageId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Tạo Trang ghi chú mới trong Tệp ghi chú.</summary>
        [HttpPost("pages")]
        public async Task<IActionResult> CreatePage([FromBody] CreateNotebookPageRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.CreatePageAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Cập nhật tiêu đề, nội dung rich text (autosave), di chuyển tệp cha hoặc đồng bộ yêu thích với kiểm soát xung đột Revision.</summary>
        [HttpPut("pages/{pageId:int}")]
        public async Task<IActionResult> UpdatePage(int pageId, [FromBody] UpdateNotebookPageRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.UpdatePageAsync(pageId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Bật/tắt đánh dấu yêu thích cho Trang ghi chú.</summary>
        [HttpPatch("pages/{pageId:int}/favorite")]
        public async Task<IActionResult> TogglePageFavorite(int pageId, [FromBody] TogglePageFavoriteRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.TogglePageFavoriteAsync(pageId, request.IsFavorite, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Xóa mềm Trang ghi chú (ẩn các mục đã lưu thuộc trang nhưng giữ nguyên mọi thẻ SRS đã tạo).</summary>
        [HttpDelete("pages/{pageId:int}")]
        public async Task<IActionResult> DeletePage(int pageId, CancellationToken cancellationToken)
        {
            var result = await notebookService.DeletePageAsync(pageId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Smart View: danh sách Trang yêu thích (không nhân bản dữ liệu).</summary>
        [HttpGet("pages/favorites")]
        public async Task<IActionResult> GetFavoritePages(CancellationToken cancellationToken)
        {
            var result = await notebookService.GetFavoritePagesAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Smart View: danh sách toàn bộ Mục đã lưu (hỗ trợ tìm kiếm, lọc loại, sắp xếp và phân trang phía server).</summary>
        [HttpGet("saved-items")]
        public async Task<IActionResult> GetSavedItems(
            [FromQuery] string? search,
            [FromQuery] string? kind,
            [FromQuery] int? pageId,
            [FromQuery] int? fileId,
            [FromQuery] int? folderId,
            [FromQuery] bool? favoriteOnly,
            [FromQuery] string? sortBy = "recent",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24,
            CancellationToken cancellationToken = default)
        {
            var result = await notebookService.GetSavedItemsAsync(search, kind, pageId, fileId, folderId, favoriteOnly, sortBy, page, pageSize, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Tìm kiếm thống nhất trên Thư mục, Tệp ghi chú, Trang và Mục đã lưu.</summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int limit = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await notebookService.SearchAsync(q, limit, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lưu nhanh đoạn văn bản/từ vựng từ Smart Text Selection vào một Trang (mặc định Sổ tay chung nếu chưa chọn).</summary>
        [HttpPost("quick-save")]
        public async Task<IActionResult> QuickSave([FromBody] QuickSaveNotebookRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.QuickSaveAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        // =========================================================================
        // Backward-Compatible Endpoints (Lists & Entries & Explicit SRS Actions)
        // =========================================================================

        /// <summary>Tạo Tệp ghi chú mới (tên endpoint legacy /lists).</summary>
        [HttpPost("lists")]
        public async Task<IActionResult> CreateList([FromBody] CreateLearnerNoteRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.CreateListAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy danh sách Tệp ghi chú của học viên.</summary>
        [HttpGet("lists")]
        public async Task<IActionResult> GetMyLists(CancellationToken cancellationToken)
        {
            var result = await notebookService.GetMyListsAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Cập nhật Tệp ghi chú.</summary>
        [HttpPut("lists/{listId:int}")]
        public async Task<IActionResult> UpdateList(int listId, [FromBody] UpdateLearnerNoteRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.UpdateListAsync(listId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Xóa mềm Tệp ghi chú.</summary>
        [HttpDelete("lists/{listId:int}")]
        public async Task<IActionResult> DeleteList(int listId, CancellationToken cancellationToken)
        {
            var result = await notebookService.DeleteListAsync(listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy các mục đã lưu thuộc một Tệp ghi chú.</summary>
        [HttpGet("lists/{listId:int}/entries")]
        public async Task<IActionResult> GetListEntries(int listId, CancellationToken cancellationToken)
        {
            var result = await notebookService.GetListEntriesAsync(listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lưu Từ vựng từ ngân hàng chung vào Sổ tay (chỉ thêm SRS khi EnrollSrs=true).</summary>
        [HttpPost("bookmark/vocabulary")]
        public async Task<IActionResult> BookmarkVocabulary([FromBody] BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.BookmarkVocabularyAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lưu Kanji từ ngân hàng chung vào Sổ tay (chỉ thêm SRS khi EnrollSrs=true).</summary>
        [HttpPost("bookmark/kanji")]
        public async Task<IActionResult> BookmarkKanji([FromBody] BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.BookmarkKanjiAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Thêm trực tiếp một điểm ngữ pháp vào hàng chờ ôn tập SRS.</summary>
        [HttpPost("bookmark/grammar/{grammarPointId:int}")]
        public async Task<IActionResult> BookmarkGrammar(int grammarPointId, CancellationToken cancellationToken)
        {
            var result = await notebookService.BookmarkGrammarAsync(grammarPointId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Thêm mục đã lưu vào Tệp / Trang ghi chú.</summary>
        [HttpPost("entries")]
        public async Task<IActionResult> AddManualEntry([FromBody] AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.AddManualEntryAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Cập nhật nội dung mục đã lưu hoặc di chuyển mục sang trang ghi chú khác.</summary>
        [HttpPut("entries/{noteEntryId:int}")]
        public async Task<IActionResult> UpdateEntry(int noteEntryId, [FromBody] UpdateNotebookEntryRequest request, CancellationToken cancellationToken)
        {
            var result = await notebookService.UpdateEntryAsync(noteEntryId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Chủ động đưa một mục đã lưu từ Sổ tay vào hàng chờ ôn tập SRS.</summary>
        [HttpPost("entries/{noteEntryId:int}/srs")]
        public async Task<IActionResult> EnrollEntryToSrs(int noteEntryId, CancellationToken cancellationToken)
        {
            var result = await notebookService.EnrollEntryToSrsAsync(noteEntryId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Xóa mềm mục đã lưu khỏi Sổ tay (không tự xóa thẻ SRS đã tạo).</summary>
        [HttpDelete("entries/{noteEntryId:int}")]
        public async Task<IActionResult> RemoveEntry(int noteEntryId, CancellationToken cancellationToken)
        {
            var result = await notebookService.RemoveEntryAsync(noteEntryId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
