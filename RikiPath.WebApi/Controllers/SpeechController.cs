using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpeechController(ISpeechToTextService speechService) : ControllerBase
    {
        /// <summary>
        /// Lấy token tạm thời của Azure Speech để FE nhận diện giọng nói trực tiếp.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình luyện nói / phát âm (Speaking Practice).
        /// - Luồng xử lý: BE dùng Subscription Key (lưu trong cấu hình, không lộ ra FE) để đổi lấy authorization token tạm thời từ Azure.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Dùng token + region + language trả về để khởi tạo Azure Speech SDK.
        ///   Token chỉ sống 10 phút (ExpiresInSeconds) nên cần gọi lại API này để làm mới trước khi hết hạn (khuyến nghị ~9 phút).
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về Token, Region, Language, ExpiresInSeconds.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server: Chưa cấu hình AzureSpeech (Region/SubscriptionKey).</response>
        /// <response code="502">Azure Speech từ chối cấp token (sai key/region hoặc Azure lỗi).</response>
        [Authorize]
        [HttpGet("get-token")]
        public async Task<IActionResult> GetToken(CancellationToken cancellationToken)
        {
            var result = await speechService.GetTokenAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}