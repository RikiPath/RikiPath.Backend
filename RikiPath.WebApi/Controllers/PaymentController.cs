using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using System.Text;
using System.Text.Json;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController(IPaymentService paymentService) : ControllerBase
    {
        private const int MaxWebhookBytes = 64 * 1024;
        private static readonly JsonSerializerOptions WebhookJson = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Webhook endpoint PayOS gọi về sau khi giao dịch có update (thanh toán thành công/failed/...)
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: N/A (webhook).
        /// - Luồng xử lý: đọc body gốc, verify chữ ký trên đúng object <c>data</c> PayOS gửi (mọi field, sắp xếp alphabet),
        ///   đối chiếu đơn hàng (mã liên kết, số tiền, tiền tệ) rồi kích hoạt quyền lợi đúng một lần.
        /// - Lưu ý cho FE/PayOS: Đây là endpoint công khai; PayOS sẽ gửi POST. Webhook giả mạo bị từ chối (400);
        ///   lỗi tạm thời trả 5xx để PayOS gửi lại.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Đã nhận webhook (có hoặc không có thay đổi đơn hàng).</response>
        /// <response code="400">Payload không hợp lệ hoặc chữ ký sai.</response>
        /// <response code="500">Lỗi tạm thời khi xử lý webhook — PayOS sẽ gửi lại.</response>
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        [RequestSizeLimit(MaxWebhookBytes)]
        public async Task<IActionResult> PayOsWebhook(CancellationToken cancellationToken)
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken);

            PayOsWebhookRequest? webhook;
            string? rawData = null;
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return BadRequest(ApiResponse<bool>.Fail("Webhook không hợp lệ."));
                if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                    rawData = data.GetRawText();
                webhook = document.RootElement.Deserialize<PayOsWebhookRequest>(WebhookJson);
            }
            catch (JsonException)
            {
                return BadRequest(ApiResponse<bool>.Fail("Webhook không hợp lệ."));
            }
            if (webhook is null || rawData is null)
                return BadRequest(ApiResponse<bool>.Fail("Webhook không hợp lệ."));

            var result = await paymentService.HandlePayOsWebhookAsync(webhook, cancellationToken, rawData);
            return StatusCode(result.StatusCode, result);
        }
    }
}
