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

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode((int)response.StatusCode, response);
}
