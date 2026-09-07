using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Consultations;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ConsultationController(IConsultationService consultationService) : ControllerBase
    {
        // Learner endpoints
        /// <summary>
        /// Đặt lịch buổi tư vấn/meeting với consultant.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Booking/Consultation trong app Learner.
        /// - Luồng xử lý: API lưu lịch trong CSDL và tạo record cuộc hẹn; có thể gửi email/SMS xác nhận và thêm vào queue của consultant.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Thời gian xử lý ~0.5-2s (do gửi thông báo). Trường thời gian phải theo ISO8601.
        /// </remarks>
        /// <param name="request">BookMeetingRequest: PracticeId (Optional), PreferredTime (Required), Notes (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết cuộc hẹn đã tạo.</response>
        /// <response code="400">Dữ liệu không hợp lệ: thời gian sai định dạng hoặc xung đột lịch.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi tạo cuộc hẹn hoặc gửi thông báo.</response>
        [HttpPost("book-meeting")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> BookMeeting([FromBody] BookMeetingRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.BookMeetingAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tạo ticket/phiếu tư vấn (không phải lịch hẹn) để support hoặc hỏi chuyên môn.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Form gửi câu hỏi/ticket trong Learner.
        /// - Luồng xử lý: Lưu ticket vào CSDL, notify consultant group, có thể gửi email thông báo; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Trường Subject và Content là Required.
        /// </remarks>
        /// <param name="request">SubmitTicketRequest: Subject (Required), Content (Required), Attachments (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về ticketId và trạng thái.</response>
        /// <response code="400">Dữ liệu không hợp lệ: thiếu Subject/Content.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi lưu ticket hoặc gửi thông báo.</response>
        [HttpPost("tickets")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> SubmitTicket([FromBody] SubmitTicketRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.SubmitTicketAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách yêu cầu/ticket của người dùng hiện tại.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang My Requests / Ticket History.
        /// - Luồng xử lý: Truy vấn CSDL; FE nên gọi kèm phân trang nếu hỗ trợ.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Response có thể lớn nên hỗ trợ pagination ở FE.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách yêu cầu/ticket của user.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("my-requests")]
        [Authorize(Roles = "Learner")]
        public async Task<IActionResult> GetMyRequests(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.GetMyRequestsAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        // Consultant endpoints
        /// <summary>
        /// Lấy queue các yêu cầu đang chờ xử lý cho consultant.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dashboard/Queue của Consultant.
        /// - Luồng xử lý: Truy vấn các ticket/chờ meeting chưa được xử lý; có thể sắp xếp theo ưu tiên.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Consultant). Hỗ trợ polling hoặc websocket để cập nhật queue.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách yêu cầu chờ xử lý.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Consultant.</response>
        /// <response code="500">Lỗi server khi truy vấn queue.</response>
        [HttpGet("consultant/queue")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetConsultantQueue(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.GetConsultantQueueAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Consultant nhận/claim một ticket để bắt đầu xử lý.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Queue -> Claim action trên UI của Consultant.
        /// - Luồng xử lý: Cập nhật record ticket owner, có thể notify learner và chuyển trạng thái.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Consultant). Hành động nên disable button sau khi gửi đến server để tránh double-claim.
        /// </remarks>
        /// <param name="requestId">ID ticket cần claim (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Ticket đã được claim bởi consultant hiện tại.</response>
        /// <response code="400">Yêu cầu không hợp lệ: ticket đã ở trạng thái không thể claim.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Consultant.</response>
        /// <response code="404">Không tìm thấy ticket.</response>
        /// <response code="500">Lỗi server khi cập nhật ticket.</response>
        [HttpPatch("consultant/requests/{requestId:int}/claim")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> ClaimTicket(int requestId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.ClaimTicketAsync(userId, requestId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Gửi câu trả lời/feedback cho ticket bởi consultant.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chi tiết ticket trên UI consultant.
        /// - Luồng xử lý: Lưu câu trả lời, có thể gửi email/notification cho learner; có thể cập nhật lịch sử meeting.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Consultant). Field Content (Required). Nếu có attachments, upload trước và gửi URL.
        /// </remarks>
        /// <param name="requestId">ID ticket (Required).</param>
        /// <param name="request">SubmitAnswerRequest: Content (Required), Attachments (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về answerId hoặc trạng thái đã gửi.</response>
        /// <response code="400">Dữ liệu không hợp lệ: content rỗng hoặc định dạng attachment sai.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Consultant.</response>
        /// <response code="404">Không tìm thấy ticket.</response>
        /// <response code="500">Lỗi server khi lưu answer hoặc gửi thông báo.</response>
        [HttpPost("consultant/requests/{requestId:int}/answer")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> SubmitAnswer(int requestId, [FromBody] SubmitAnswerRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.SubmitAnswerAsync(userId, requestId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Ghi chú (note) buổi tư vấn bởi consultant (log meeting note).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chi tiết meeting/ticket trên UI consultant.
        /// - Luồng xử lý: Lưu note vào lịch sử cuộc họp; không gửi mail tự động trừ khi có cấu hình.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Consultant). Nội dung note là Required.
        /// </remarks>
        /// <param name="requestId">ID ticket/meeting (Required).</param>
        /// <param name="request">LogMeetingNoteRequest: Content (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Note được lưu.</response>
        /// <response code="400">Dữ liệu không hợp lệ: nội dung rỗng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Consultant.</response>
        /// <response code="404">Không tìm thấy ticket/meeting.</response>
        /// <response code="500">Lỗi server khi lưu note.</response>
        [HttpPost("consultant/requests/{requestId:int}/log-note")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> LogMeetingNote(int requestId, [FromBody] LogMeetingNoteRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await consultationService.LogMeetingNoteAsync(userId, requestId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
