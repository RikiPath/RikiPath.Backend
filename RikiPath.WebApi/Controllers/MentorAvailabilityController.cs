using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/mentor/availability")]
[Authorize(Roles = "Mentor")]
public sealed class MentorAvailabilityController(IMentorMeetingService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyAvailabilityAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMentorAvailabilityRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.CreateAvailabilityAsync(request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => ToActionResult(await service.DeleteAvailabilityAsync(id, cancellationToken));

    [HttpPost("request-cancel")]
    public async Task<IActionResult> RequestCancel(
        [FromBody] CancelAvailabilitiesRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.RequestCancelAvailabilitiesAsync(request, cancellationToken));

    [HttpGet("/api/mentor/meetings")]
    public async Task<IActionResult> GetMyMeetings(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyMeetingsAsync(cancellationToken));

    [HttpGet("/api/mentor/meetings/{bookingId:int}")]
    public async Task<IActionResult> GetBooking(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await service.GetMentorBookingAsync(bookingId, cancellationToken));

    [HttpPost("/api/mentor/meetings/{bookingId:int}/accept")]
    public async Task<IActionResult> AcceptBooking(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await service.AcceptMentorBookingAsync(bookingId, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode((int)response.StatusCode, response);
}
