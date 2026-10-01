using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;

namespace RikiPath.WebApi.Controllers;

/// <summary>HTTP endpoints for Mentor availability and the Mentor's paid meeting schedule.</summary>
[ApiController]
[Route("api/mentor/availability")]
[Authorize(Roles = "Mentor")]
public sealed class MentorAvailabilityController(IMentorMeetingService service) : ControllerBase
{
    /// <summary>Lists availability slots owned by the authenticated Mentor.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyAvailabilityAsync(cancellationToken));

    /// <summary>Lists the authenticated Mentor's paid meeting bookings and room links.</summary>
    [HttpGet("/api/mentor/meetings")]
    public async Task<IActionResult> GetMyMeetings(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyMeetingsAsync(cancellationToken));

    /// <summary>Creates an open future availability slot for the authenticated Mentor.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMentorAvailabilityRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.CreateAvailabilityAsync(request, cancellationToken));

    /// <summary>Soft-deletes an unbooked availability slot owned by the authenticated Mentor.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => ToActionResult(await service.DeleteAvailabilityAsync(id, cancellationToken));

    private IActionResult ToActionResult<T>(RikiPath.Application.Responses.ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
