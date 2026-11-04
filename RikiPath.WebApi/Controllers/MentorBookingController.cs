using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.MentorCompensation;
using System.Net;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
public sealed class MentorBookingController(IMentorBookingService bookingService) : ControllerBase
{
    [HttpGet("api/learner/mentor-bookings")]
    [Authorize(Roles = "Learner")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorBookingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var result = await bookingService.GetMyBookingsAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/mentor-bookings")]
    [Authorize(Roles = "Mentor")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorBookingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMentorBookings(CancellationToken cancellationToken)
    {
        var result = await bookingService.GetMyMentorBookingsAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/mentor-bookings/{bookingId:int}")]
    [Authorize(Roles = "Mentor")]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMentorBooking(int bookingId, CancellationToken cancellationToken)
    {
        var result = await bookingService.GetMentorBookingAsync(bookingId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên đặt lịch giữ chỗ nguyên tử từ số dư tín dụng khả dụng kèm trừ vé FIFO theo đúng trình độ JLPT và bảo vệ chống trùng lịch
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
        if (string.IsNullOrWhiteSpace(request.JlptLevel) ||
            !MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out _))
        {
            var bad = ApiResponse<MentorBookingResponse>.Fail(
                "Vui lòng chọn trình độ JLPT hợp lệ (N5, N4, N3 hoặc N2; không hỗ trợ N1).",
                HttpStatusCode.BadRequest);
            return StatusCode(bad.StatusCode, bad);
        }

        var result = await bookingService.BookSlotAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên hoặc Quản trị viên giải phóng giữ chỗ / hủy lịch hẹn, phục hồi lượt tín dụng và slot khả dụng
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
