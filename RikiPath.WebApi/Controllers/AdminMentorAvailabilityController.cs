using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

/// <summary>
/// HTTP endpoints dành cho Admin quản lý và kiểm duyệt lịch làm việc của Mentor.
/// </summary>
[ApiController]
[Route("api/admin/mentor-availability")]
[Route("api/admin/mentor-availabilities")]
[Authorize(Roles = "Admin")]
public sealed class AdminMentorAvailabilityController(IMentorMeetingService service, IMentorAvailabilityReviewService reviewService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách các khung giờ trống mới đăng ký đang chờ Admin phê duyệt.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAvailabilities(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPendingAvailabilitiesAsync(cancellationToken));
    [HttpGet("mentor/{mentorId:int}/schedule")]
    public async Task<IActionResult> GetSchedule(int mentorId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var response = await reviewService.GetScheduleAsync(mentorId, from, to, ct);
        return StatusCode(response.StatusCode, response);
    }
    [HttpGet]
    public async Task<IActionResult> GetSlots(CancellationToken cancellationToken)
    {
        var response = await reviewService.GetSlotsAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Phê duyệt hoặc từ chối một hoặc nhiều khung giờ trống mới do Mentor đăng ký.
    /// </summary>
    [HttpPost("review")]
    public async Task<IActionResult> ReviewAvailabilities(
        [FromBody] List<int> ids,
        [FromQuery] bool approve,
        [FromQuery] string? rejectionReason,
        CancellationToken cancellationToken)
        => ToActionResult(await service.ReviewAvailabilitiesAsync(ids, approve, rejectionReason, cancellationToken));

    /// <summary>
    /// Phê duyệt hoặc từ chối một hoặc nhiều yêu cầu hủy slot trống từ Mentor.
    /// </summary>
    [HttpPost("review-cancel")]
    public async Task<IActionResult> ReviewCancelAvailabilities(
        [FromBody] List<int> ids,
        [FromQuery] bool approve,
        [FromQuery] string? rejectionReason,
        CancellationToken cancellationToken)
        => ToActionResult(await service.ReviewCancelAvailabilitiesAsync(ids, approve, rejectionReason, cancellationToken));

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewMentorAvailabilityRequest request, CancellationToken ct)
    {
        var response = await reviewService.ReviewAsync(id, request.Approve, request.Reason, ct);
        return StatusCode(response.StatusCode, response);
    }
    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode((int)response.StatusCode, response);
}
public sealed class ReviewMentorAvailabilityRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public required bool Approve { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? Reason { get; init; }
}