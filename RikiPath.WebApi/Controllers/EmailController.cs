using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class EmailController(IEmailService emailService) : ControllerBase
    {
        public record SendValidationEmailRequest(string ToEmail, string HtmlContent);

        [HttpPost("send-validation")]
        public async Task<IActionResult> SendValidationEmail([FromBody] SendValidationEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await emailService.SendValidationEmailAsync(request.ToEmail, request.HtmlContent, cancellationToken);
            return StatusCode(result.IsSuccess ? 200 : 500, result);
        }
    }
}
