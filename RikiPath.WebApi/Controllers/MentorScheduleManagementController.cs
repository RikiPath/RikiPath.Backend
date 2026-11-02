using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers
{
    // // Các API cho Chuyên gia tự quản lý mẫu lịch làm việc, quy tắc ca lặp và sinh/công bố khung giờ 90 phút.
    [ApiController]
    [Route("api/v1/mentors/me/schedule")]
    [Route("api/mentor/schedule")]
    public sealed class MentorScheduleManagementController(
        IMentorScheduleManagementService scheduleService) : ControllerBase
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
        [HttpPost("preview")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> PreviewSlots(
            [FromBody] PreviewSlotsRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.PreviewSlotsAsync(request, cancellationToken));

        // // Sinh và công bố chính thức các khung giờ làm việc khả dụng vào hệ thống đặt lịch.
        // Đảm bảo tính bất biến (idempotent), không trùng GenerationKey và không ghi đè buổi đã có học viên đặt.
        // Yêu cầu tài khoản Chuyên gia phải có hồ sơ ĐÃ ĐƯỢC PHÊ DUYỆT (Approved).
        [HttpPost("publish-slots")]
        [HttpPost("publish")]
        [HttpPost("generate-slots")]
        [Authorize(Roles = "Mentor")]
        public async Task<IActionResult> PublishSlots(
            [FromBody] PublishSlotsRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.PublishSlotsAsync(request, cancellationToken));

        // // Hủy hoặc đóng một khung giờ làm việc tương lai chưa có học viên đặt lịch.
        [HttpDelete("slots/{slotId:int}")]
        [HttpPatch("slots/{slotId:int}/disable")]
        [Authorize(Roles = "Mentor,Admin")]
        public async Task<IActionResult> DisableSlot(
            int slotId,
            [FromBody] DisableSlotRequest? request,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.DisableSlotAsync(slotId, request, cancellationToken));

        // // Dành cho Quản trị viên: Xem mẫu lịch làm việc và quy tắc ca của một Chuyên gia bất kỳ.
        [HttpGet("/api/v1/admin/mentors/{mentorId:int}/schedule")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMentorScheduleForAdmin(
            int mentorId,
            CancellationToken cancellationToken)
            => ToActionResult(await scheduleService.GetMentorScheduleByIdForAdminAsync(mentorId, cancellationToken));

        private IActionResult ToActionResult<T>(ApiResponse<T> response)
            => StatusCode(response.StatusCode, response);
    }
}
