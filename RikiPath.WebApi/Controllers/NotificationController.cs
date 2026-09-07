using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class NotificationController(INotificationService notificationService) : ControllerBase
    {
        /// <summary>
        /// Lấy danh sách thông báo của người dùng (có thể lọc onlyUnread).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Notification center / Bell dropdown trong ứng dụng Learner.
        /// - Luồng xử lý: Truy vấn CSDL/Cache; hỗ trợ phân trang. Không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. FE nên hiển thị badge count dựa trên unreadCount. Sử dụng polling hoặc WebSocket cho real-time.
        /// </remarks>
        /// <param name="onlyUnread">Chỉ lấy thông báo chưa đọc (Optional, default=false).</param>
        /// <param name="page">Số trang (Optional, default=1).</param>
        /// <param name="pageSize">Kích thước trang (Optional, default=20).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách thông báo (và metadata phân trang).</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi truy vấn thông báo.</response>
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool onlyUnread = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.GetMyNotificationsAsync(userId, onlyUnread, page, pageSize, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy số lượng thông báo chưa đọc của user.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Hiển thị badge số thông báo trên UI.
        /// - Luồng xử lý: Truy vấn cache hoặc CSDL, trả về số lượng unread nhanh.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Gọi thường xuyên có thể sử dụng cache TTL để giảm tải.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về số lượng thông báo chưa đọc.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi truy vấn.</response>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu một thông báo là đã đọc.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Khi người dùng mở thông báo hoặc click vào item.
        /// - Luồng xử lý: Cập nhật trạng thái read trong CSDL; có thể giảm counter unread trong cache.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. FE nên cập nhật UI local ngay khi gọi và rollback khi lỗi.
        /// </remarks>
        /// <param name="notificationId">ID thông báo cần đánh dấu (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Thông báo được đánh dấu đã đọc.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Thông báo không thuộc user.</response>
        /// <response code="404">Không tìm thấy thông báo.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái.</response>
        [HttpPatch("{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo của user là đã đọc.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chức năng "Mark all as read" trong Notification center.
        /// - Luồng xử lý: Cập nhật nhiều record trong CSDL/cache; có thể là thao tác tốn tài nguyên, backend có thể xử lý bất đồng bộ.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. FE nên disable button sau khi gửi để tránh gọi nhiều lần.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Tất cả thông báo đã được đánh dấu đọc.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi cập nhật (có thể do kích thước dataset lớn).</response>
        [HttpPatch("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
