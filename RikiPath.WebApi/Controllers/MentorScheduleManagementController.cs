using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain.MentorScheduling;
using System.Net;

namespace RikiPath.WebApi.Controllers
{
    // // Các API cho Chuyên gia tự quản lý mẫu lịch làm việc, quy tắc ca lặp và sinh/công bố khung giờ 90 phút.
    [ApiController]
    [Route("api/mentors/me/schedule")]
    public sealed class MentorScheduleManagementController(
        IMentorScheduleManagementService scheduleService,
        IMentorGroupPolicyService groupPolicies) : ControllerBase
    {
        // // Lấy thông tin mẫu lịch làm việc và danh sách quy tắc ca lặp của Chuyên gia đang đăng nhập.
        [HttpGet]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> GetMySchedule(CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.GetMyScheduleAsync(cancellationToken));

        // // Tạo mới hoặc cập nhật toàn bộ mẫu lịch làm việc kèm các quy tắc ca lặp tuần của Chuyên gia.
        [HttpPut]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> UpdateMySchedule(
            [FromBody] UpdateMentorScheduleRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.UpdateMyScheduleAsync(request, cancellationToken));

        // // Lấy danh sách các quy tắc ca làm việc lặp theo tuần của Chuyên gia đang đăng nhập.
        [HttpGet("rules")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> GetMyScheduleRules(CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.GetMyScheduleRulesAsync(cancellationToken));

        // // Cập nhật danh sách các quy tắc ca làm việc lặp theo tuần của Chuyên gia đang đăng nhập.
        [HttpPut("rules")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> UpdateMyScheduleRules(
            [FromBody] List<MentorScheduleRuleRequest> rules,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.UpdateMyScheduleRulesAsync(rules, cancellationToken));

        // // Xem trước danh sách khung giờ 90 phút (kèm 15 phút nghỉ) sẽ được sinh ra từ mẫu lịch.
        // Yêu cầu tài khoản Chuyên gia phải có hồ sơ ĐÃ ĐƯỢC PHÊ DUYỆT (Approved).
        [HttpPost("preview-slots")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> PreviewSlots(
            [FromBody] PreviewSlotsRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.PreviewSlotsAsync(request, cancellationToken));

        // Sinh khung giờ theo chính sách và gửi Admin duyệt; Mentor không tự công bố.
        [HttpPost("publish-slots")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> PublishSlots(
            [FromBody] PublishSlotsRequest request,
            CancellationToken cancellationToken)
        {
            if (request.StartDate.Year < 2020)
                return BadRequest(ApiResponse<PublishSlotsResponse>.Fail("Ngày bắt đầu không hợp lệ."));
            var localMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(
                request.StartDate.ToDateTime(TimeOnly.MinValue),
                TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));
            if (await groupPolicies.GetEffectiveAsync(localMidnightUtc, cancellationToken) is null)
                return Conflict(ApiResponse<PublishSlotsResponse>.Fail(
                    "Chưa có chính sách vận hành áp dụng cho lịch này; chưa thể tạo buổi học.", HttpStatusCode.Conflict));
            return ToActionResult(await scheduleService.PublishSlotsAsync(request, cancellationToken));
        }

        /// <summary>
        /// Mentor xem trước xung đột lịch dạy của chính mình (trùng giờ, thời gian nghỉ, giới hạn buổi/ngày và buổi/tuần)
        /// cho các khung giờ dự kiến. Chỉ mang tính tham khảo; hệ thống vẫn kiểm tra lại khi ghi lịch.
        /// </summary>
        [HttpPost("conflict-preview")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> PreviewTeachingConflicts(
            [FromBody] TeachingConflictPreviewRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.PreviewTeachingConflictsAsync(request, cancellationToken));

        // // Hủy hoặc đóng một khung giờ làm việc tương lai chưa có học viên đặt lịch.
        [HttpDelete("slots/{slotId:int}")]
        [HttpPatch("slots/{slotId:int}/disable")]
        [Authorize(Roles = "Mentor,Admin")]
        public IActionResult DisableSlot(
            int slotId,
            [FromBody] DisableSlotRequest? request,
            CancellationToken cancellationToken)
            => Conflict(new { ErrorMessage = "Lịch đã duyệt cần gửi yêu cầu dời hoặc hủy để Quản trị viên xử lý." });

        // // Dành cho Quản trị viên: Xem mẫu lịch làm việc và quy tắc ca của một Chuyên gia bất kỳ.
        [HttpGet("/api/admin/mentors/{mentorId:int}/schedule")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMentorScheduleForAdmin(
            int mentorId,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.GetMentorScheduleByIdForAdminAsync(mentorId, cancellationToken));

        private IActionResult ToActionResult<T>(ApiResponse<T> response)
            => StatusCode(response.StatusCode, response);
    }
}
