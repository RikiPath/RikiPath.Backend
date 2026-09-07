using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LearningPathController(ILearningPathService learningPathService) : ControllerBase
    {
        /// <summary>
        /// Tạo/lấy learning path cá nhân cho người dùng (có thể gọi AI để sinh đề xuất).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình tạo lộ trình học cá nhân (Learning Path) trên app Learner.
        /// - Luồng xử lý: API có thể gọi dịch vụ AI (AiLearningPathClient) ngầm để sinh đề xuất dựa trên profile/goal của user, lưu vào CSDL và trả về kết quả.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Gọi có thể mất 1-5s do xử lý AI; FE hãy hiển thị loading và hỗ trợ hủy yêu cầu nếu cần.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về learning path JSON cho user.</response>
        /// <response code="400">Yêu cầu không hợp lệ: thiếu thông tin profile cần thiết.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="422">Bị từ chối do giới hạn AI hoặc quota.</response>
        /// <response code="500">Lỗi server hoặc lỗi dịch vụ AI bên thứ ba.</response>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await learningPathService.GenerateLearningPathAsync(userId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }
    }
}
