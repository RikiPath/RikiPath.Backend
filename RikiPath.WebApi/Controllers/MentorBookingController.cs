using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
public sealed class MentorBookingController(IMentorBookingService bookingService) : ControllerBase
{
    // Học viên đặt lịch giữ chỗ nguyên tử từ số dư tín dụng khả dụng kèm trừ vé FIFO và bảo vệ chống trùng lịch
    [HttpPost("api/v1/learner/mentor-bookings")]
    [HttpPost("api/learner/mentor-bookings")]
    [Authorize(Roles = "Learner")]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookSlot(
        [FromBody] BookIncludedMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.BookSlotAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên hoặc Quản trị viên giải phóng giữ chỗ / hủy lịch hẹn, phục hồi lượt tín dụng và slot khả dụng
    [HttpPost("api/v1/learner/mentor-bookings/{bookingId:int}/release")]
    [HttpPost("api/learner/mentor-bookings/{bookingId:int}/release")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReleaseBookingHold(
        [FromRoute] int bookingId,
        [FromBody] ReleaseBookingHoldRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.ReleaseBookingHoldAsync(bookingId, request?.Reason, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
