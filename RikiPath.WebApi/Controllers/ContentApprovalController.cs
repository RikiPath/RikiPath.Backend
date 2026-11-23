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
    public class ContentApprovalController(
        IContentApprovalService approvalService,
        IContentModerationV2Service moderationV2,
        IClaimService claimService) : ControllerBase
    {

        /// <summary>
        /// Gửi danh sách dòng dữ liệu đã xem trước vào hàng chờ phê duyệt (Biên tập viên / Admin).
        /// </summary>
        [HttpPost("submit-import")]
        [Authorize(Roles = "Admin,ContentAuthor")]
        [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status410Gone)]
        public IActionResult SubmitImportRequests()
        {
            return StatusCode(StatusCodes.Status410Gone,
                ApiResponse<int>.Fail(
                    "Luồng gửi diff trực tiếp đã đóng. Hãy lưu bản nháp bằng /api/content-import/confirm rồi gửi duyệt từ bản nháp.",
                    System.Net.HttpStatusCode.Gone));
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

        [HttpGet("v2/queue")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetModerationQueue(
            [FromQuery] ContentApprovalStatus? status,
            [FromQuery] ImportEntityType? entityType,
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await moderationV2.ListQueueAsync(status, entityType, keyword, page, pageSize, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("v2/requests/{requestId:int}/detail")]
        [Authorize(Roles = "Admin,ContentAuthor")]
        public async Task<IActionResult> GetModerationDetail(int requestId, CancellationToken cancellationToken)
        {
            var result = await moderationV2.GetDetailAsync(requestId, cancellationToken);
            if (result.IsSuccess && result.Result is not null &&
                !User.IsInRole("Admin") &&
                result.Result.Summary.AuthorUserId != claimService.GetUserClaim().Id)
            {
                return Forbid();
            }
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("v2/requests/{requestId:int}/moderate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ModerateV2(
            int requestId,
            [FromBody] ModerateContentV2Request request,
            CancellationToken cancellationToken)
        {
            var user = claimService.GetUserClaim();
            var adminEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var adminName = string.IsNullOrWhiteSpace(adminEmail) ? (User.Identity?.Name ?? $"Admin #{user.Id}") : adminEmail;
            var result = await moderationV2.ModerateAsync(requestId, request, user.Id, adminName, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("v2/unpublish")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnpublishV2(
            [FromBody] UnpublishContentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await moderationV2.UnpublishAsync(request, claimService.GetUserClaim().Id, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
