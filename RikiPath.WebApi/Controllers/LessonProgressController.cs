using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Lessons;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LessonProgressController(ILessonProgressService lessonProgressService) : ControllerBase
    {
        /// <summary>
        /// Lấy danh sách bài học khả dụng và tiến độ của Learner theo cấp độ JLPT.
        /// </summary>
        /// <remarks>
        /// Dùng ID cấp độ thật từ catalog Backend. Kết quả được sắp xếp theo SortOrder rồi Id.
        /// Catalog bài học rỗng vẫn trả 200 với danh sách rỗng; không thay bằng dữ liệu mẫu.
        /// </remarks>
        /// <param name="certificateLevelId">ID CertificateLevel JLPT (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Danh sách bài học và tiến độ hiện tại của Learner.</response>
        /// <response code="400">ID cấp độ không hợp lệ.</response>
        /// <response code="401">Thiếu Bearer Token.</response>
        /// <response code="403">Token không có role Learner.</response>
        /// <response code="404">Cấp độ JLPT không khả dụng.</response>
        /// <response code="500">Không thể truy vấn nội dung học.</response>
        [HttpGet("lessons")]
        [ProducesResponseType(typeof(ApiResponse<LearningDiscoveryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LearningDiscoveryResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<LearningDiscoveryResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<LearningDiscoveryResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLessons(
            [FromQuery] int certificateLevelId,
            CancellationToken cancellationToken)
        {
            var result = await lessonProgressService.GetLearningDiscoveryAsync(
                certificateLevelId,
                cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

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
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLessonDetail(int lessonId, CancellationToken cancellationToken)
        {

            var result = await lessonProgressService.GetLessonDetailAsync(lessonId, cancellationToken);
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
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdatePlaybackPosition(int lessonId, [FromBody] UpdatePlaybackPositionRequest request, CancellationToken cancellationToken)
        {

            var result = await lessonProgressService.UpdatePlaybackPositionAsync(lessonId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đánh dấu bài học đã hoàn thành cho Learner hiện tại.
        /// </summary>
        /// <remarks>
        /// Identity lấy từ JWT, request không nhận userId. Gọi lại cùng lesson là idempotent
        /// và không tạo thêm LessonProgress trùng.
        /// </remarks>
        /// <param name="lessonId">ID bài học khả dụng (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Đã hoàn thành hoặc bài học đã được hoàn thành trước đó.</response>
        /// <response code="401">Thiếu Bearer Token.</response>
        /// <response code="403">Token không có role Learner.</response>
        /// <response code="404">Bài học không tồn tại hoặc không khả dụng.</response>
        /// <response code="500">Không thể lưu trạng thái hoàn thành.</response>
        [HttpPost("lessons/{lessonId:int}/complete")]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<LessonDetailResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompleteLesson(
            int lessonId,
            CancellationToken cancellationToken)
        {
            var result = await lessonProgressService.CompleteLessonAsync(
                lessonId,
                cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
