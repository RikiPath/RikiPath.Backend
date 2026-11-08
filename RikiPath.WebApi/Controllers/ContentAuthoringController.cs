using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;

namespace RikiPath.WebApi.Controllers
{
    /// <summary>API AI hỗ trợ content author soạn và kiểm tra nội dung (Kanji trước; Vocabulary/
    /// GrammarPattern/Lesson/Practice Exercise sẽ bổ sung theo cùng pattern).</summary>
    [ApiController]
    [Route("api/content-authoring")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public sealed class ContentAuthoringController(
        IContentAuthoringService service) : ControllerBase
    {
        /// <summary>
        /// Content author gõ yêu cầu tự do, AI đề xuất danh sách Kanji phù hợp và hệ thống tự dò trùng
        /// với Kanji đã có trong DB. CHƯA lưu gì — chỉ trả về để preview/chỉnh sửa trước khi xác nhận.
        /// </summary>
        /// <response code="200">Trả về danh sách Kanji AI đề xuất, kèm đánh dấu trùng (nếu có).</response>
        /// <response code="400">Thiếu prompt hoặc AI trả về kết quả không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="404">Không tìm thấy Lesson (nếu có truyền LessonId).</response>
        /// <response code="429">AI tạm thời bị giới hạn hạn mức.</response>
        /// <response code="500">Không thể tạo đề xuất lúc này.</response>
        [HttpPost("kanji/suggest")]
        [ProducesResponseType(typeof(ApiResponse<SuggestKanjiResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SuggestKanjiResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<SuggestKanjiResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<SuggestKanjiResponse>), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApiResponse<SuggestKanjiResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SuggestKanji(
            [FromBody] SuggestKanjiRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.SuggestKanjiAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Content author xác nhận danh sách Kanji (có thể đã chỉnh sửa tay sau khi preview). Hệ thống
        /// ghi vào ContentApprovalRequest với trạng thái Draft để Admin duyệt theo đúng luồng hiện có.
        /// </summary>
        /// <response code="200">Đã gửi duyệt thành công.</response>
        /// <response code="400">Danh sách gửi lên trống hoặc thiếu dữ liệu bắt buộc.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="500">Không thể gửi duyệt lúc này.</response>
        [HttpPost("kanji/confirm")]
        [ProducesResponseType(typeof(ApiResponse<SubmitKanjiApprovalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SubmitKanjiApprovalResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<SubmitKanjiApprovalResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConfirmKanji(
            [FromBody] ConfirmKanjiSuggestionsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ConfirmKanjiSuggestionsAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Quét lại nội dung content author hiện tại đã gửi, đang ở trạng thái Draft (chưa Admin duyệt),
        /// nhờ AI đối chiếu với kiến thức chuẩn và chỉ rõ từng chỗ sai (nếu có) để tự sửa trước khi Admin
        /// xem. Truyền batchId để chỉ quét 1 lần gửi cụ thể, bỏ trống để quét toàn bộ Draft hiện có.
        /// </summary>
        /// <response code="200">Trả về báo cáo kiểm tra từng dòng nội dung.</response>
        /// <response code="400">Không có nội dung Draft nào để kiểm tra.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="500">Không thể kiểm tra nội dung lúc này.</response>
        [HttpPost("scan-accuracy")]
        [ProducesResponseType(typeof(ApiResponse<ScanContentAccuracyResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ScanContentAccuracyResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<ScanContentAccuracyResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ScanAccuracy(
            [FromBody] ScanContentAccuracyRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ScanMyDraftContentAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}