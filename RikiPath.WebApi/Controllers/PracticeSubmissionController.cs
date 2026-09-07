using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Practice;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PracticeSubmissionController(IPracticeSubmissionService practiceSubmissionService) : ControllerBase
    {
        /// <summary>
        /// Nộp bài luyện tập (bài tự luận hoặc ảnh) để lưu và (nếu có) yêu cầu chấm tự động.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình nộp bài luyện tập (Practice / Submission) trong ứng dụng Learner.
        /// - Luồng nghiệp vụ: API lưu bản ghi nộp bài vào CSDL, sau đó có thể gọi ngầm dịch vụ chấm (AI grading) để trả về điểm và phản hồi. Có thể cập nhật tiến trình học hoặc lịch sử làm bài của người dùng.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Gọi có thể mất 1-5s nếu có chấm AI; hiển thị spinner/đang xử lý. Body là JSON, trường bắt buộc tối thiểu: PracticeId và (TextContent hoặc ImageUrl). Các enum/định dạng file phải theo tài liệu schema của SubmitPracticeRequest.
        /// </remarks>
        /// <param name="request">Yêu cầu nộp bài. Required: PracticeId (int), ít nhất phải có TextContent (string) hoặc ImageUrl (string). Optional: Attachment, Metadata.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về kết quả nộp bài và (nếu có) kết quả chấm/feedback.</response>
        /// <response code="400">Dữ liệu không hợp lệ: Thiếu PracticeId hoặc không có nội dung nộp.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Tài khoản không có role Learner.</response>
        /// <response code="422">Bị từ chối do giới hạn (ví dụ hết quota AI cho ngày hôm nay).</response>
        /// <response code="500">Lỗi server hoặc lỗi dịch vụ chấm (AI) bên thứ ba.</response>
        [HttpPost("submit")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> Submit([FromBody] SubmitPracticeRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceSubmissionService.SubmitAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy chi tiết một bài nộp theo ID.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình xem chi tiết nộp bài hoặc modal chi tiết kết quả chấm.
        /// - Luồng nghiệp vụ: Trả về thông tin nộp bài, điểm chấm, feedback, trạng thái chấm, và metadata liên quan. Không gọi AI nữa — chỉ đọc từ CSDL.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu bài không thuộc người dùng và user không phải admin, trả 403/404 tuỳ chính sách.
        /// </remarks>
        /// <param name="submissionId">ID của bài nộp (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết bài nộp (including grading, feedback).</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền truy cập bài này (nếu bài thuộc user khác).</response>
        /// <response code="404">Không tìm thấy bài nộp với submissionId này.</response>
        /// <response code="500">Lỗi server khi đọc dữ liệu.</response>
        [HttpGet("{submissionId:int}")]
        [Authorize]
        public async Task<IActionResult> GetById(int submissionId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceSubmissionService.GetByIdAsync(userId, submissionId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy lịch sử nộp bài của người dùng hiện tại.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Lịch sử luyện tập / My Submissions.
        /// - Luồng nghiệp vụ: Truy vấn danh sách các nộp bài của user, có phân trang/lọc nếu cần (FE nên gọi kèm tham số phân trang nếu endpoint hỗ trợ).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Kết quả có thể lớn, nên FE cần hỗ trợ lazy-loading/pagination; response thường trả nhanh (<200ms) nếu có index trên DB.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách nộp bài (có thể kèm metadata phân trang).</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi truy vấn lịch sử.</response>
        [HttpGet("my-history")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> GetMyHistory(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceSubmissionService.GetMyHistoryAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Yêu cầu chấm lại một nộp bài (regrade) bởi hệ thống/AI.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Tính năng Regrade trên chi tiết nộp bài (Learner).
        /// - Luồng nghiệp vụ: Gửi yêu cầu chấm lại; API có thể enqueue job hoặc gọi trực tiếp dịch vụ chấm AI. Có thể cập nhật trạng thái chấm và gửi thông báo khi xong.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Thao tác có thể mất vài giây (nếu chấm đồng bộ) hoặc trả nhanh + gửi thông báo khi hoàn tất (nếu chấm bất đồng bộ). FE nên hiển thị trạng thái "Đang chấm lại" sau khi gọi.
        /// </remarks>
        /// <param name="submissionId">ID của bài nộp cần chấm lại (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Yêu cầu chấm lại đã được nhận/hoàn tất: Trả về trạng thái mới hoặc kết quả.</response>
        /// <response code="400">Dữ liệu không hợp lệ: submissionId không hợp lệ hoặc bài chưa trong trạng thái cho phép regrade.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Người dùng không sở hữu bài hoặc không được phép regrade.</response>
        /// <response code="404">Không tìm thấy bài nộp tương ứng.</response>
        /// <response code="429">Bị giới hạn: Regrade bị chặn do quota hoặc giới hạn tốc độ.</response>
        /// <response code="500">Lỗi server hoặc lỗi dịch vụ chấm (AI) bên thứ ba.</response>
        [HttpPost("{submissionId:int}/regrade")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> Regrade(int submissionId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceSubmissionService.RegradeAsync(userId, submissionId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
