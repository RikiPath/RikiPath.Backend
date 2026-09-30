using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses.Payments;
using RikiPath.Application.Responses;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController(IPaymentService paymentService) : ControllerBase
    {
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
    }
}
