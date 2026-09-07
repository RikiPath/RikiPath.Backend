using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class ReviewController(IReviewService reviewService) : ControllerBase
    {
        /// <summary>
        /// Lấy danh sách review hàng ngày (SM-2 queue) cho người dùng.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang Daily Review / Flashcards của Learner.
        /// - Luồng xử lý: Tính toán SM-2 hoặc thuật toán lặp lại cách quãng (spaced repetition) để trả về các item cần ôn; có thể đọc từ cache/DB.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Kết quả có thể trả theo batch; FE nên hiển thị loading và chuẩn bị UI cho answer/feedback.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách item cần review hôm nay.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi tính toán queue (thuật toán SM-2).</response>
        [HttpGet("daily-queue")]
        public async Task<IActionResult> GetDailyQueue(CancellationToken cancellationToken)
        {
            var response = await reviewService.GetDailyReviewQueueAsync(GetCurrentUserId(), cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Gửi kết quả ôn tập (answer) cho một review item.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Khi người dùng trả lời 1 item trên Daily Review.
        /// - Luồng xử lý: Cập nhật điểm/interval theo SM-2, lưu lịch sử, có thể cập nhật progress/course completion.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Request nên gửi điểm/quality (ví dụ 0-5). Trả về trạng thái cập nhật ngay.
        /// </remarks>
        /// <param name="request">SubmitReviewRequest: ItemId (Required), Quality (Required numeric 0-5), ResponseTime (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về interval/gap mới và trạng thái item.</response>
        /// <response code="400">Dữ liệu không hợp lệ: quality ngoài khoảng cho phép.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: item không thuộc user.</response>
        /// <response code="500">Lỗi server khi cập nhật thuật toán SM-2.</response>
        [HttpPost("submit-result")]
        public async Task<IActionResult> SubmitResult([FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
        {
            var response = await reviewService.SubmitReviewResultAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
