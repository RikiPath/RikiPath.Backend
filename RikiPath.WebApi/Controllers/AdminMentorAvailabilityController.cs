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
[Authorize(Roles = "Admin")]
public sealed class AdminMentorMeetingsController(IMentorMeetingService service) : ControllerBase
{
    /// <summary>Phê duyệt hoặc từ chối yêu cầu hủy slot trống của Mentor.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAvailabilities(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPendingAvailabilitiesAsync(cancellationToken));

    /// <summary>Phê duyệt hoặc từ chối khung giờ trống Mentor mới đăng ký.</summary>
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> ReviewAvailability(
        int id,
        [FromQuery] bool approve,
        [FromQuery] string? rejectionReason,
        CancellationToken cancellationToken)
        => ToActionResult(await service.ReviewAvailabilityAsync(id, approve, rejectionReason, cancellationToken));

    /// <summary>Phê duyệt hoặc từ chối yêu cầu hủy slot trống của Mentor.</summary>
    [HttpPost("{id:int}/review-cancel")]
    public async Task<IActionResult> ReviewCancelAvailability(
        int id,
        [FromQuery] bool approve,
        [FromQuery] string? rejectionReason,
        CancellationToken cancellationToken)
        => ToActionResult(await service.ReviewCancelAvailabilityAsync(id, approve, rejectionReason, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}