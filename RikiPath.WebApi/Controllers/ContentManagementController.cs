using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Domain;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ContentManagementController(IContentManagementService contentService) : ControllerBase
    {
        /// <summary>
        /// Nhập nội dung hàng loạt (bulk import) từ file/JSON vào hệ thống.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang quản lý nội dung của Content Author (Import tool).
        /// - Luồng xử lý: API nhận payload import, validate, lưu tạm/batch vào CSDL, có thể enqueue job xử lý background để tạo các bản ghi. Có thể gửi email/notification sau khi import hoàn tất.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = ContentAuthor). Payload có thể lớn; FE nên sử dụng upload file và hiển thị tiến trình. Có thể trả response 202 Accepted nếu xử lý bất đồng bộ.
        /// </remarks>
        /// <param name="request">BulkImportRequest: Source (Required), Items (Required) hoặc FileUrl (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về số bản ghi đã import hoặc chi tiết kết quả nếu xử lý đồng bộ.</response>
        /// <response code="202">Accepted: Yêu cầu được nhận và sẽ xử lý nền (background job).</response>
        /// <response code="400">Dữ liệu không hợp lệ: format import sai hoặc thiếu trường bắt buộc.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role ContentAuthor.</response>
        /// <response code="500">Lỗi server khi lưu batch hoặc enqueue job.</response>
        [HttpPost("bulk-import")]
        [Authorize(Roles = "ContentAuthor")]
        public async Task<IActionResult> BulkImport([FromBody] BulkImportRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await contentService.BulkImportAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Gửi một nội dung cụ thể để duyệt (submit for review).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Nút Submit for Review trên trang chỉnh sửa nội dung của Content Author.
        /// - Luồng xử lý: Cập nhật trạng thái nội dung sang PendingReview, thông báo cho Admin/Reviewer (email/notification).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = ContentAuthor). Thao tác nhanh; FE nên hiện trạng thái "Đã gửi" sau khi nhận 200.
        /// </remarks>
        /// <param name="entityType">Loại entity (Required, enum ContentEntityType).</param>
        /// <param name="entityId">ID entity cần gửi review (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Nội dung chuyển sang trạng thái chờ duyệt.</response>
        /// <response code="400">Dữ liệu không hợp lệ: entityId không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role ContentAuthor.</response>
        /// <response code="404">Không tìm thấy entity.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái hoặc gửi thông báo.</response>
        [HttpPost("submit/{entityType}/{entityId:int}")]
        [Authorize(Roles = "ContentAuthor")]
        public async Task<IActionResult> SubmitForReview(ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await contentService.SubmitForReviewAsync(userId, entityType, entityId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách nội dung do người dùng hiện tại tạo theo loại entity.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang quản lý nội dung của Content Author (My Content).
        /// - Luồng xử lý: Truy vấn CSDL theo userId và entityType; hỗ trợ lọc/trạng thái nếu cũ.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = ContentAuthor). FE nên hỗ trợ phân trang nếu backend trả nhiều kết quả.
        /// </remarks>
        /// <param name="entityType">Loại entity (Required, enum ContentEntityType).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách nội dung của user theo loại.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role ContentAuthor.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("my-content/{entityType}")]
        [Authorize(Roles = "ContentAuthor")]
        public async Task<IActionResult> GetMyContent(ContentEntityType entityType, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await contentService.GetMyContentAsync(userId, entityType, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách nội dung đang chờ duyệt theo loại (Admin view).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dashboard của Admin/Reviewer để duyệt nội dung.
        /// - Luồng xử lý: Truy vấn các bản ghi có trạng thái PendingReview; có thể kèm phân trang/lọc theo author.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Kết quả có thể lớn; FE nên hỗ trợ pagination và search.
        /// </remarks>
        /// <param name="entityType">Loại entity (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách bản ghi pending review.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Admin.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("reviews/pending/{entityType}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingReview(ContentEntityType entityType, CancellationToken cancellationToken)
        {
            var result = await contentService.GetPendingReviewAsync(entityType, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Duyệt nội dung (approve/reject) bởi Admin.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Modal/Panel review của Admin.
        /// - Luồng xử lý: Cập nhật trạng thái nội dung, ghi log reviewer, gửi thông báo cho author nếu cần.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Field Action trong body (approve/reject) là Required.
        /// </remarks>
        /// <param name="entityType">Loại entity (Required).</param>
        /// <param name="entityId">ID entity (Required).</param>
        /// <param name="request">ReviewContentRequest: Action (Required), Comment (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trạng thái đã được cập nhật và thông báo gửi (nếu có).</response>
        /// <response code="400">Dữ liệu không hợp lệ: action sai định dạng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Admin.</response>
        /// <response code="404">Không tìm thấy entity.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái hoặc gửi thông báo.</response>
        [HttpPost("reviews/{entityType}/{entityId:int}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Review(ContentEntityType entityType, int entityId, [FromBody] ReviewContentRequest request, CancellationToken cancellationToken)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await contentService.ReviewAsync(adminId, entityType, entityId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
