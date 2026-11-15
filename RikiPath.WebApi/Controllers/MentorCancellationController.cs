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
public sealed class MentorCancellationController(IMentorCancellationService cancellationService) : ControllerBase
{
    // Hủy lịch hẹn tư vấn có hỗ trợ phân tầng chính sách (>= 24h vs < 24h) và hoàn vé
    [HttpPost("api/mentor-bookings/{bookingId:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelBooking(
        [FromRoute] int bookingId,
        [FromBody] CancelMentorBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cancellationService.CancelBookingAsync(bookingId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Dời lịch hẹn sang một khung giờ khả dụng khác của cùng Mentor (chỉ áp dụng >= 24h)
    [HttpPost("api/mentor-bookings/{bookingId:int}/reschedule")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorRescheduleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorRescheduleResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorRescheduleResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorRescheduleResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorRescheduleResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleBooking(
        [FromRoute] int bookingId,
        [FromBody] RescheduleMentorBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cancellationService.RescheduleBookingAsync(bookingId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Kiểm tra điều kiện hoàn vé và dời lịch theo chính sách thời gian thực
    [HttpGet("api/mentor-bookings/{bookingId:int}/cancellation-eligibility")]
    [ProducesResponseType(typeof(ApiResponse<CancellationPolicyEligibilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CancellationPolicyEligibilityResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<CancellationPolicyEligibilityResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckCancellationEligibility(
        [FromRoute] int bookingId,
        CancellationToken cancellationToken)
    {
        var result = await cancellationService.CheckCancellationEligibilityAsync(bookingId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên xét duyệt yêu cầu ngoại lệ hoàn vé cho các ca hủy sát giờ (< 24h)
    [HttpPost("api/admin/mentor-bookings/{bookingId:int}/review-cancellation")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorCancellationResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewCancellationException(
        [FromRoute] int bookingId,
        [FromBody] ReviewCancellationExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cancellationService.ReviewExceptionRequestAsync(bookingId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
