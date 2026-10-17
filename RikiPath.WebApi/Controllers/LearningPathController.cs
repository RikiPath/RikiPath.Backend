using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningPath;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LearningPathController(ILearningPathService learningPathService) : ControllerBase
    {
        /// <summary>
        /// Tạo đề xuất lộ trình học cá nhân hóa bằng AI cho người dùng.
        /// </summary>
        /// <remarks>
        /// Màn hình sử dụng: Màn hình tạo lộ trình học cá nhân (Learning Path) trên app Learner.
        /// 
        /// Luồng xử lý: API gọi dịch vụ AI ngầm để sinh đề xuất dựa trên trình độ/mục tiêu của người học (theo ngày, tích hợp tùy chọn từ vựng, kanji, ngữ pháp), lưu vào CSDL và trả về kết quả.
        /// 
        /// Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Quá trình gọi có thể mất từ 1-5s do xử lý AI; FE nên hiển thị trạng thái loading và hỗ trợ cancellationToken.
        /// </remarks>
        /// <param name="request">Thông tin cấu hình lộ trình học (isBeginner, targetCertification, durationDays, includeVocabulary, includeKanji, includeGrammar).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về lộ trình học tập được thiết kế bởi AI.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Thiếu thông tin profile hoặc định dạng JSON không đúng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="429">Bị từ chối do vượt quá hạn ngạch gọi AI (Too Many Requests / Quota Exceeded).</response>
        /// <response code="500">Lỗi server hoặc dịch vụ AI không phản hồi.</response>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            [FromBody] GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
        {
            var response = await learningPathService.GenerateLearningPathAsync(
                request,
                cancellationToken);

            return StatusCode((int)response.StatusCode, response);
        }
    }
}