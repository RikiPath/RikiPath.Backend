using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;

namespace RikiPath.WebApi.Controllers;

/// <summary>HTTP endpoints for Mentor Meeting plans, availability, purchases, and bookings.</summary>
[ApiController]
[Route("api/mentor-meetings")]
public sealed class MentorMeetingController(IMentorMeetingService service) : ControllerBase
{
    /// <summary>Lists active Mentor Meeting plans and their included Features.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPlansAsync(cancellationToken));

    /// <summary>Lists available Mentor slots within a date range (up to 90 days).</summary>
    [HttpGet("availability")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? mentorId,
        CancellationToken cancellationToken)
        => ToActionResult(await service.GetAvailableSlotsAsync(from, to, mentorId, cancellationToken));

    /// <summary>Reserves the selected slot and returns a PayOS checkout link for the chosen plan.</summary>
    [HttpPost("purchase")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseMentorPlanRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.PurchaseAsync(request, cancellationToken));

    /// <summary>Books an additional session using an already paid meeting package.</summary>
    [HttpPost("subscriptions/{subscriptionId:int}/bookings")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> BookIncludedSession(
        int subscriptionId,
        [FromBody] BookIncludedMeetingRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.BookIncludedSessionAsync(subscriptionId, request, cancellationToken));

    /// <summary>Lists the authenticated learner's Mentor bookings.</summary>
    [HttpGet("bookings")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyBookingsAsync(cancellationToken));

    /// <summary>Returns payment and booking status for a meeting package purchase.</summary>
    [HttpGet("purchases/{subscriptionId:int}")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetPurchaseStatus(int subscriptionId, CancellationToken cancellationToken)
        => ToActionResult(await service.GetPurchaseStatusAsync(subscriptionId, cancellationToken));

    private IActionResult ToActionResult<T>(RikiPath.Application.Responses.ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
