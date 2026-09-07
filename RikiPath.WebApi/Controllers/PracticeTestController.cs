using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeTests;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class PracticeTestController(IPracticeTestService practiceTestService) : ControllerBase
    {
        /// <summary>
        /// Bắt đầu một lần làm bài kiểm tra (practice test) cho user.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Start Test / Quiz trong app Learner.
        /// - Luồng xử lý: Tạo attempt mới trong CSDL, có thể sinh câu hỏi ngẫu nhiên hoặc lấy đề đã cấu hình; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). FE nên hiển thị countdown/timeout nếu test có giới hạn thời gian.
        /// </remarks>
        /// <param name="practiceTestId">ID practice test (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về attemptId và thông tin cần thiết để bắt đầu.</response>
        /// <response code="400">Dữ liệu không hợp lệ: test không còn khả dụng hoặc đã hết hạn đăng ký.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner hoặc user không được phép tham gia test.</response>
        /// <response code="500">Lỗi server khi tạo attempt.</response>
        [HttpPost("{practiceTestId:int}/start")]
        public async Task<IActionResult> StartAttempt(int practiceTestId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.StartAttemptAsync(userId, practiceTestId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nộp kết quả một lần làm bài (submit attempt).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Nút Submit trên trang làm bài kiểm tra.
        /// - Luồng xử lý: Tính điểm, lưu kết quả; có thể gọi dịch vụ chấm tự động (AI) cho dạng tự luận.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu có phần tự luận chấm AI, thao tác có thể mất thêm thời gian (1-5s).
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="request">SubmitAttemptRequest: Answers (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về kết quả tóm tắt (score, status) hoặc chi tiết nếu có.</response>
        /// <response code="400">Dữ liệu không hợp lệ: answers thiếu hoặc format sai.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="500">Lỗi server khi tính điểm hoặc gọi dịch vụ chấm.</response>
        [HttpPost("attempts/{attemptId:int}/submit")]
        public async Task<IActionResult> SubmitAttempt(int attemptId, [FromBody] SubmitAttemptRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.SubmitAttemptAsync(userId, attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy kết quả tóm tắt của một attempt.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang Result / History của Learner.
        /// - Luồng xử lý: Truy vấn kết quả đã lưu; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu attempt chưa chấm xong (đang chờ AI), FE có thể hiển thị trạng thái pending.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về summary result (score, status).</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("attempts/{attemptId:int}")]
        public async Task<IActionResult> GetAttemptResult(int attemptId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.GetAttemptResultAsync(userId, attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách practice tests theo JLPT level.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang chọn bài kiểm tra theo cấp độ JLPT.
        /// - Luồng xử lý: Truy vấn CSDL các test được gán cho jlptLevelId.
        /// - Lưu ý cho FE: Không bắt buộc Bearer Token (tùy policy), nếu public có thể bỏ token. FE nên xử lý trường hợp không có tests.
        /// </remarks>
        /// <param name="jlptLevelId">JLPT level ID (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách tests phù hợp.</response>
        /// <response code="400">Dữ liệu không hợp lệ: jlptLevelId không hợp lệ.</response>
        /// <response code="404">Không tìm thấy tests cho level này.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("level/{jlptLevelId:int}/tests")]
        public async Task<IActionResult> GetTestsByLevel(int jlptLevelId, CancellationToken cancellationToken)
        {
            var result = await practiceTestService.GetTestsByLevelAsync(jlptLevelId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy kết quả chi tiết của một attempt (per-question breakdown, feedback).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang detailed result (show per-question feedback, explanations).
        /// - Luồng xử lý: Truy vấn dữ liệu kết quả đã lưu; nếu có feedback AI, có thể bao gồm text trả về từ dịch vụ AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Response có thể lớn tuỳ số câu hỏi; FE nên lazy-load hoặc collapse sections.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về kết quả chi tiết và feedback.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="500">Lỗi server khi truy vấn hoặc demarshal feedback.</response>
        [HttpGet("attempts/{attemptId:int}/detailed")]
        public async Task<IActionResult> GetDetailedResult(int attemptId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.GetDetailedResultAsync(userId, attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
