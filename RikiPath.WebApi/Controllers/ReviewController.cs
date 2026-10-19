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
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Response gồm mặt trước và đáp án; FE cần ẩn đáp án đến khi người học chọn xem.
        /// - FE có thể giữ danh sách Items làm phiên hiện tại, chuyển sang item tiếp theo sau khi gửi kết quả.
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
        /// - Luồng xử lý: Cập nhật điểm/interval theo SM-2 và lưu lịch sử.
        /// - Lưu ý cho FE: Quality nhận 1-5. Nếu ShouldRepeatInSession=true, đưa thẻ sai vào cuối hàng retry của phiên hiện tại.
        /// </remarks>
        /// <param name="request">SubmitReviewRequest: ReviewCardId (Required), Quality (Required numeric 1-5).</param>
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
