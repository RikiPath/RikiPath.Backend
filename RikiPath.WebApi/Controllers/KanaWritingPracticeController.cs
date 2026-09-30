using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.KanaWriting;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers
{
    /// <summary>API luyện viết Hiragana và Katakana dành cho người học.</summary>
    [Route("api/kana-writing-practice")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class KanaWritingPracticeController(IKanaWritingPracticeService service) : ControllerBase
    {
        /// <summary>Lấy Kana đến hạn ôn trước, sau đó bổ sung ký tự mới đã được Admin duyệt.</summary>
        /// <param name="count">Số ký tự muốn lấy, từ 1 đến 100.</param>
        /// <param name="type">Lọc bảng chữ Hiragana hoặc Katakana; bỏ trống để lấy cả hai.</param>
        /// <param name="cancellationToken">Token hủy request.</param>
        [HttpGet("due")]
        public async Task<IActionResult> GetDue([FromQuery] int count = 10, [FromQuery] KanaCharacterType? type = null, CancellationToken cancellationToken = default)
        {
            var response = await service.GetDueForPracticeAsync(count, type, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>Lưu kết quả luyện viết Kana và cập nhật lịch ôn SM-2 của người học.</summary>
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SubmitKanaWritingResultRequest request, CancellationToken cancellationToken)
        {
            var response = await service.SubmitResultAsync(request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }
    }
}
