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
    /// <summary>
    /// Lấy danh sách các khung giờ trống mới đăng ký đang chờ Admin phê duyệt.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAvailabilities(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPendingAvailabilitiesAsync(cancellationToken));

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

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode((int)response.StatusCode, response);
}