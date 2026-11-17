using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorKpi;

namespace RikiPath.WebApi.Controllers;

/// <summary>
/// API Chấm công, thời lượng giảng dạy và KPI của Cố vấn (không chứa dữ liệu lương/thù lao).
/// </summary>
[ApiController]
[Authorize]
public sealed class MentorKpiController(IMentorKpiService mentorKpiService) : ControllerBase
{
    [HttpGet("api/mentor/kpi/summary")]
    [Authorize(Roles = "Mentor")]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyKpiSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var result = await mentorKpiService.GetMyKpiSummaryAsync(from, to, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/admin/mentors/kpi")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorKpiSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorKpiSummaryResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorKpiSummaryResponse>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminMentorKpis(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? mentorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mentorKpiService.GetAdminMentorKpisAsync(from, to, mentorId, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/admin/mentors/{mentorId:int}/kpi")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorKpiSummaryResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminMentorKpiByMentor(
        [FromRoute] int mentorId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var result = await mentorKpiService.GetAdminMentorKpiByMentorAsync(mentorId, from, to, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/admin/bookings/{bookingId:int}/attendance")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingAttendanceDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingAttendanceDetailResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorBookingAttendanceDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminBookingAttendance(
        [FromRoute] int bookingId,
        CancellationToken cancellationToken = default)
    {
        var result = await mentorKpiService.GetAdminBookingAttendanceAsync(bookingId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
