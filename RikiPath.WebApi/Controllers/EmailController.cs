using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class EmailController(IEmailService emailService) : ControllerBase
    {
        public record SendValidationEmailRequest(string ToEmail, string HtmlContent);

        /// <summary>
        /// Gửi email xác thực/validation đến địa chỉ email chỉ định.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dùng khi cần gửi lại email xác thực hoặc gửi nội dung HTML tùy chỉnh cho user (Admin tool).
        /// - Luồng xử lý: API gọi service gửi email (MailKit/SMTP) để dispatch email HTML; có thể retry/ghi log khi lỗi.
        /// - Lưu ý cho FE: Thao tác này thường được gọi từ backend/admin; nếu FE gọi trực tiếp, cần Bearer Token với quyền phù hợp. Gọi có thể mất 0.5-2s tùy SMTP provider.
        /// </remarks>
        /// <param name="request">SendValidationEmailRequest: ToEmail (Required), HtmlContent (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Email đã được gửi.</response>
        /// <response code="400">Dữ liệu không hợp lệ: email format sai hoặc nội dung rỗng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token (nếu API yêu cầu).</response>
        /// <response code="500">Lỗi server hoặc gửi email thất bại.</response>
        [HttpPost("send-validation")]
        public async Task<IActionResult> SendValidationEmail([FromBody] SendValidationEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await emailService.SendValidationEmailAsync(request.ToEmail, request.HtmlContent, cancellationToken);
            return StatusCode(result.IsSuccess ? 200 : 500, result);
        }
    }
}
