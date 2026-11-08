using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;

namespace RikiPath.WebApi.Controllers
{
    /// <summary>
    /// API lập kế hoạch nội dung bằng AI cho content author, theo 3 bước:
    /// Plan (AI chuyển yêu cầu tự do thành danh sách công việc) → Execute (sinh nội dung từng việc, chưa lưu DB)
    /// → Confirm (ghi cả gói vào ContentApprovalRequest, trạng thái Draft, cùng một BatchId).
    /// </summary>
    [ApiController]
    [Route("api/content-authoring/plan")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public sealed class ContentPlanningController(
        IContentPlanningService service) : ControllerBase
    {
        /// <summary>
        /// Bước 1: content author gõ yêu cầu tự do, AI chỉ lập kế hoạch (danh sách công việc), CHƯA sinh nội dung.
        /// Nếu thiếu thông tin bắt buộc, kết quả có ClarifyingQuestion và Tasks rỗng — author trả lời rồi gửi lại.
        /// </summary>
        /// <response code="200">Trả về kế hoạch (hoặc câu hỏi làm rõ).</response>
        /// <response code="400">Thiếu prompt.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="429">AI tạm thời bị giới hạn hạn mức.</response>
        /// <response code="500">Không thể lập kế hoạch lúc này.</response>
        /// <response code="502">AI trả về kế hoạch không hợp lệ.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ContentPlanResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContentPlanResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<ContentPlanResponse>), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApiResponse<ContentPlanResponse>), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(ApiResponse<ContentPlanResponse>), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Plan(
            [FromBody] PlanContentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.PlanContentAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Bước 2: chạy kế hoạch (author có thể đã sửa/xóa/thêm công việc) để AI sinh nội dung từng việc theo thứ tự
        /// Kanji → Vocabulary → Grammar → Lesson → PracticeExercise → MockTest. CHƯA lưu gì vào DB.
        /// Mỗi công việc có trạng thái riêng (Succeeded/Failed/Skipped); một việc lỗi không làm hỏng cả gói.
        /// </summary>
        /// <response code="200">Trả về kết quả từng công việc.</response>
        /// <response code="400">Kế hoạch trống hoặc không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="500">Không thể chạy kế hoạch lúc này.</response>
        [HttpPost("execute")]
        [ProducesResponseType(typeof(ApiResponse<ExecuteContentPlanResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ExecuteContentPlanResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<ExecuteContentPlanResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Execute(
            [FromBody] ExecuteContentPlanRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ExecutePlanAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Bước 3: author xác nhận bản cuối (có thể đã chỉnh tay sau khi xem kết quả). Hệ thống ghi toàn bộ gói vào
        /// ContentApprovalRequest với trạng thái Draft cùng một BatchId để Admin duyệt.
        /// </summary>
        /// <response code="200">Đã gửi duyệt thành công, trả về BatchId và số dòng đã ghi.</response>
        /// <response code="400">Gói trống hoặc dữ liệu/liên kết không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role ContentAuthor.</response>
        /// <response code="500">Không thể gửi duyệt lúc này.</response>
        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ApiResponse<SubmitContentApprovalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SubmitContentApprovalResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<SubmitContentApprovalResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmContentPlanRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.ConfirmPlanAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}