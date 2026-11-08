using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Api.Controllers
{
    [ApiController]
    [Route("api/content-approvals")]
    [Authorize]
    public class ContentApprovalController(IContentApprovalService approvalService) : ControllerBase
    {

        /// <summary>
        /// Gửi danh sách dòng dữ liệu đã xem trước vào hàng chờ phê duyệt (Biên tập viên / Admin).
        /// </summary>
        [HttpPost("submit-import")]
        [Authorize(Roles = "Admin,ContentAuthor")]
        [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SubmitImportRequests(
            [FromBody] List<SheetImportResponse> sheetDiffs,
            CancellationToken cancellationToken)
        {
            var result = await approvalService.SubmitImportRequestsAsync(sheetDiffs, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách các yêu cầu đang chờ phê duyệt kèm thông tin Diff chi tiết (Dành cho Admin).
        /// </summary>
        [HttpGet("mine")]
        [Authorize(Roles = "Admin,ContentAuthor")]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            var result = await approvalService.GetMyApprovalsAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<List<PendingApprovalItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPendingApprovals(
            [FromQuery] ImportEntityType? entityType,
            CancellationToken cancellationToken)
        {
            var result = await approvalService.GetPendingApprovalsAsync(entityType, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Admin thực hiện Phê duyệt (Approve) hoặc Từ chối (Reject) một yêu cầu cụ thể.
        /// </summary>
        [HttpPost("requests/{requestId:int}/review")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ReviewRequest(
            [FromRoute] int requestId,
            [FromBody] ReviewContentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await approvalService.ReviewRequestAsync(requestId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("requests/{requestId:int}/review-lesson-group")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<BulkReviewResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ReviewLessonGroup(
            [FromRoute] int requestId,
            [FromBody] ReviewContentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await approvalService.ReviewLessonGroupAsync(requestId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Admin thực hiện Phê duyệt hoặc Từ chối hàng loạt yêu cầu cùng lúc.
        /// </summary>
        [HttpPost("requests/batch-review")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<BulkReviewResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ReviewBatch(
            [FromBody] BulkReviewRequest request,
            CancellationToken cancellationToken)
        {
            var result = await approvalService.ReviewBatchAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
