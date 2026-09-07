using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses.Payments;
using RikiPath.Application.Responses;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PaymentController(IPaymentService paymentService) : ControllerBase
    {
        /// <summary>
        /// Tạo payment cho consultation: tạo ConsultationPurchase (status = Pending) và trả về payment link từ PayOS.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Thanh toán khi learner đặt mua buổi tư vấn.
        /// - Luồng xử lý: API tạo record purchase với trạng thái Pending, gọi PayOS để tạo payment link và trả về cho FE.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Trường dữ liệu trong request phải theo ràng buộc business.
        /// </remarks>
        /// <param name="request">CreateConsultationPaymentRequest: thông tin purchase cần tạo.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: trả về ConsultationPaymentResponse chứa payment link và thông tin purchase.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi tạo payment hoặc gọi PayOS.</response>
        [HttpPost("consultation")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> CreateConsultationPayment([FromBody] CreateConsultationPaymentRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await paymentService.CreateConsultationPaymentAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Webhook endpoint PayOS gọi về sau khi giao dịch có update (thanh toán thành công/failed/...)
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: N/A (webhook).
        /// - Luồng xử lý: Verify chữ ký payload từ PayOS, cập nhật trạng thái Payment/ConsultationPurchase tương ứng.
        /// - Lưu ý cho FE/PayOS: Đây là endpoint công khai; PayOS sẽ gửi POST. Thực hiện verify ở service.
        /// </remarks>
        /// <param name="webhook">PayOsWebhookRequest payload từ PayOS.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Server đã xử lý webhook.</response>
        /// <response code="400">Payload không hợp lệ hoặc verify thất bại.</response>
        /// <response code="500">Lỗi server khi xử lý webhook.</response>
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOsWebhook([FromBody] PayOsWebhookRequest webhook, CancellationToken cancellationToken)
        {
            var result = await paymentService.HandlePayOsWebhookAsync(webhook, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Learner kiểm tra trạng thái giao dịch (polling hoặc xem lại lịch sử).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang chi tiết giao dịch / lịch sử mua hàng.
        /// - Luồng xử lý: Trả về trạng thái hiện tại của ConsultationPurchase (Pending, Success, Failed, ...).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Nếu đang polling, gọi định kỳ cho đến khi trạng thái cuối cùng.
        /// </remarks>
        /// <param name="purchaseId">Id của purchase cần kiểm tra.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: trả về ConsultationPurchaseStatusResponse.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="404">Không tìm thấy purchase.</response>
        /// <response code="500">Lỗi server khi truy vấn trạng thái.</response>
        [HttpGet("purchases/{purchaseId:int}/status")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> GetPurchaseStatus([FromRoute] int purchaseId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await paymentService.GetPurchaseStatusAsync(userId, purchaseId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
