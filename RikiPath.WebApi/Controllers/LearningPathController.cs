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
        /// <summary>Lấy lộ trình học gần nhất đã được tạo cho Learner hiện tại.</summary>
        /// <remarks>Không tự gọi AI. Result là null khi người học chưa từng tạo lộ trình.</remarks>
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Lấy lộ trình ACTIVE; tương đương GET /latest.</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Lịch sử lộ trình của Learner, gồm ACTIVE và SUPERSEDED.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetHistoryAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Xem trước thay đổi cấp độ mà không thay thế lộ trình đang ACTIVE.</summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewLearningPathRequest request, CancellationToken cancellationToken)
        {
            var response = await learningPathService.PreviewLearningPathAsync(request, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Xác nhận preview và kích hoạt atomically; retry cùng ID trả lại bản ACTIVE.</summary>
        [HttpPost("previews/{previewId:int}/confirm")]
        public async Task<IActionResult> Confirm(int previewId, CancellationToken cancellationToken)
        {
            var response = await learningPathService.ConfirmPreviewAsync(previewId, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Hủy preview; không thay đổi lộ trình hiện tại.</summary>
        [HttpDelete("previews/{previewId:int}")]
        public async Task<IActionResult> Cancel(int previewId, CancellationToken cancellationToken)
        {
            var response = await learningPathService.CancelPreviewAsync(previewId, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

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
        /// <param name="request">Object rỗng; Backend lấy mục tiêu và cấp độ bắt đầu từ hồ sơ đã lưu.</param>
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
