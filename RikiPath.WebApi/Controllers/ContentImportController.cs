using System.Security.Claims;
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
    [Route("api/content-import")]
    [Authorize] // Bắt buộc đăng nhập (Có thể thêm role nếu cần, vd: [Authorize(Roles = "ContentAuthor,Admin")])
    public class ContentImportController(IExcelContentImportService importService) : ControllerBase
    {
        /// <summary>
        /// Upload file Excel để xem trước (Preview) sự thay đổi và kiểm tra lỗi trước khi nhập chính thức.
        /// </summary>
        /// <param name="file">File Excel (.xlsx, .xls)</param>
        /// <param name="entityType">Tùy chọn: Chọn loại nội dung cụ thể nếu chỉ muốn đọc 1 sheet duy nhất</param>
        /// <param name="cancellationToken"></param>
        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ImportPreviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportPreviewResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Preview(
            IFormFile file,
            [FromQuery] ImportEntityType? entityType,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<ImportPreviewResponse>.Fail("Vui lòng chọn file Excel để tải lên."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xls"))
            {
                return BadRequest(ApiResponse<ImportPreviewResponse>.Fail("Định dạng file không hỗ trợ. Vui lòng tải lên file .xlsx hoặc .xls."));
            }

            await using var stream = file.OpenReadStream();
            var result = await importService.PreviewAsync(stream, file.FileName, entityType, cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xác nhận (Confirm) danh sách dữ liệu sau khi Preview để tạo bản nháp (Draft) chờ Admin duyệt.
        /// </summary>
        /// <param name="request">Danh sách dòng New/Modified do Frontend gửi lên</param>
        /// <param name="cancellationToken"></param>
        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ApiResponse<ImportConfirmResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportConfirmResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmImportRequest request,
            CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();
            if (actorUserId is null)
            {
                return Unauthorized(ApiResponse<ImportConfirmResponse>.Fail("Không xác định được thông tin người dùng."));
            }

            var result = await importService.ConfirmAsync(request, actorUserId.Value, cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Trích xuất UserId của người dùng hiện tại từ Claims/Token JWT
        /// </summary>
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : null;
        }
    }
}