using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class NotebookController(INotebookService notebookService) : ControllerBase
    {
        /// <summary>
        /// Tạo danh sách từ vựng/kanji mới cho người dùng.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang Notebook -> Tạo danh sách mới (Create List) trong app Learner.
        /// - Luồng xử lý: Lưu danh sách vào CSDL liên kết với user; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Trường Title là Required; Items có thể optional. Response trả nhanh.
        /// </remarks>
        /// <param name="request">CreateVocabularyListRequest: Title (Required), Description (Optional), Items (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách đã tạo.</response>
        /// <response code="400">Dữ liệu không hợp lệ: thiếu Title hoặc format Items sai.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu danh sách.</response>
        [HttpPost("lists")]
        public async Task<IActionResult> CreateList([FromBody] CreateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.CreateListAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách notebook (vocabulary lists) của user.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Notebook -> My Lists.
        /// - Luồng xử lý: Truy vấn CSDL; có thể hỗ trợ phân trang/filters.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. FE nên hỗ trợ pagination nếu backend trả nhiều kết quả.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách notebook của user.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("lists")]
        public async Task<IActionResult> GetMyLists(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.GetMyListsAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật thông tin danh sách từ vựng (title, description, items).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chỉnh sửa notebook list.
        /// - Luồng xử lý: Cập nhật record trong CSDL; đảm bảo user sở hữu list.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Trường Title nếu thay đổi phải không rỗng.
        /// </remarks>
        /// <param name="listId">ID danh sách (Required).</param>
        /// <param name="request">UpdateVocabularyListRequest: Title (Optional), Items (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Danh sách đã được cập nhật.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: User không sở hữu list này.</response>
        /// <response code="404">Không tìm thấy list.</response>
        /// <response code="500">Lỗi server khi cập nhật dữ liệu.</response>
        [HttpPut("lists/{listId:int}")]
        public async Task<IActionResult> UpdateList(int listId, [FromBody] UpdateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.UpdateListAsync(userId, listId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Xóa một danh sách từ vựng của user.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng xóa list trong Notebook.
        /// - Luồng xử lý: Xóa record và entries liên quan trong CSDL; có thể soft-delete tùy cấu hình.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Hiển thị confirm dialog trước khi gọi API.
        /// </remarks>
        /// <param name="listId">ID danh sách cần xóa (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Danh sách đã được xóa.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: User không sở hữu list này.</response>
        /// <response code="404">Không tìm thấy list.</response>
        /// <response code="500">Lỗi server khi xóa.</response>
        [HttpDelete("lists/{listId:int}")]
        public async Task<IActionResult> DeleteList(int listId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.DeleteListAsync(userId, listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy các mục/entries trong một danh sách cụ thể.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Hiển thị nội dung của một notebook list.
        /// - Luồng xử lý: Truy vấn entries theo listId; có thể hỗ trợ phân trang.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu list private, FE cần kiểm tra quyền sở hữu.
        /// </remarks>
        /// <param name="listId">ID danh sách (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách entries.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền truy cập list này.</response>
        /// <response code="404">Không tìm thấy list.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("lists/{listId:int}/entries")]
        public async Task<IActionResult> GetListEntries(int listId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.GetListEntriesAsync(userId, listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu từ vựng vào notebook (bookmark vocabulary).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Tính năng bookmark từ vựng/kanji trong lesson hoặc search results.
        /// - Luồng xử lý: Thêm mục vào notebook của user; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Trường VocabularyId hoặc text là Required tuỳ request model.
        /// </remarks>
        /// <param name="request">BookmarkVocabularyRequest: VocabularyId (Required) hoặc Text (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Item đã được thêm vào notebook.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu dữ liệu.</response>
        [HttpPost("bookmark/vocabulary")]
        public async Task<IActionResult> BookmarkVocabulary([FromBody] BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkVocabularyAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu (bookmark) 1 Kanji vào notebook.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng bookmark Kanji trong bài học hoặc tra cứu Kanji.
        /// - Luồng xử lý: Thêm Kanji vào notebook user; có thể lưu thông tin JLPT level.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Trường KanjiId hoặc Character là Required tuỳ model.
        /// </remarks>
        /// <param name="request">BookmarkKanjiRequest: KanjiId (Required) hoặc Character (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Kanji đã được bookmark.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu dữ liệu.</response>
        [HttpPost("bookmark/kanji")]
        public async Task<IActionResult> BookmarkKanji([FromBody] BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkKanjiAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu một điểm ngữ pháp vào notebook.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng bookmark grammar point trong lesson.
        /// - Luồng xử lý: Thêm record liên kết user-grammar vào CSDL.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Gọi nhanh, trả status ngay.
        /// </remarks>
        /// <param name="grammarPointId">ID điểm ngữ pháp (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Grammar point đã được bookmark.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu dữ liệu.</response>
        [HttpPost("bookmark/grammar/{grammarPointId:int}")]
        public async Task<IActionResult> BookmarkGrammar(int grammarPointId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkGrammarAsync(userId, grammarPointId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Thêm mục ghi chú thủ công vào notebook.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng thêm note/manual entry trong Notebook.
        /// - Luồng xử lý: Lưu note vào CSDL dưới user hiện tại.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Content là Required; có thể hỗ trợ attachments nhưng nên upload trước qua FileStorage API.
        /// </remarks>
        /// <param name="request">AddManualEntryRequest: Content (Required), Title (Optional), Attachments (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Entry đã được thêm.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu note.</response>
        [HttpPost("entries")]
        public async Task<IActionResult> AddManualEntry([FromBody] AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.AddManualEntryAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Xóa một mục ghi chú trong notebook.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng xóa note trong Notebook.
        /// - Luồng xử lý: Xóa record nếu user sở hữu entry; có thể soft-delete.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Hiển thị confirm trước khi gọi.
        /// </remarks>
        /// <param name="noteEntryId">ID mục cần xóa (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Entry đã được xóa.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: User không sở hữu entry.</response>
        /// <response code="404">Không tìm thấy entry.</response>
        /// <response code="500">Lỗi server khi xóa.</response>
        [HttpDelete("entries/{noteEntryId:int}")]
        public async Task<IActionResult> RemoveEntry(int noteEntryId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.RemoveEntryAsync(userId, noteEntryId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
