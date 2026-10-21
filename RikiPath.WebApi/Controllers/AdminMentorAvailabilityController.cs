using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/admin/mentor-availabilities")]
[Authorize(Roles = "Admin")]
public sealed class AdminMentorAvailabilityController(IMentorAvailabilityReviewService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSlots(CancellationToken cancellationToken)
    {
        var response = await service.GetSlotsAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewMentorAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ReviewAsync(id, request.Approve, request.Reason, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}

public sealed class ReviewMentorAvailabilityRequest
{
    [Required]
    public required bool Approve { get; init; }
    [MaxLength(1000)]
    public string? Reason { get; init; }
}
