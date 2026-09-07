using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LessonProgressController(ILessonProgressService lessonProgressService) : ControllerBase
    {
        /// <summary>
        /// Lấy chi tiết tiến độ và nội dung bài học cho user tại lessonId.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang lesson detail trong app Learner (video, transcript, exercises).
        /// - Luồng xử lý: Truy vấn tiến độ, playback position, điểm hoàn thành; không gọi AI. Có thể cập nhật xem lần cuối.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Trả về thông tin playback, completed, resources; FE nên lazy-load media URL.
        /// </remarks>
        /// <param name="lessonId">ID bài học (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết bài học và trạng thái tiến độ của user.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="404">Không tìm thấy lesson này.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("lessons/{lessonId:int}")]
        public async Task<IActionResult> GetLessonDetail(int lessonId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await lessonProgressService.GetLessonDetailAsync(userId, lessonId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật vị trí phát (playback position) của user trong lesson.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Khi người dùng xem video/lesson, FE gọi để lưu checkpoint.
        /// - Luồng xử lý: Cập nhật vị trí phát vào CSDL/Cache để resume sau; không gây tác động lớn khác.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Gọi thường xuyên (throttling) nên FE nên gửi không quá tần suất cao (ví dụ mỗi vài giây tối thiểu).
        /// </remarks>
        /// <param name="lessonId">ID lesson (Required).</param>
        /// <param name="request">UpdatePlaybackPositionRequest: PositionInSeconds (Required), Duration (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Vị trí phát đã được lưu.</response>
        /// <response code="400">Dữ liệu không hợp lệ: position âm hoặc vượt quá duration.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="404">Không tìm thấy lesson.</response>
        /// <response code="500">Lỗi server khi lưu tiến độ.</response>
        [HttpPatch("lessons/{lessonId:int}/playback-position")]
        public async Task<IActionResult> UpdatePlaybackPosition(int lessonId, [FromBody] UpdatePlaybackPositionRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await lessonProgressService.UpdatePlaybackPositionAsync(userId, lessonId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
