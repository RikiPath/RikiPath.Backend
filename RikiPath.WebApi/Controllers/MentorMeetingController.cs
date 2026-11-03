using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/mentor-meetings")]
public sealed class MentorMeetingController(IMentorMeetingService service) : ControllerBase
{
    [HttpGet("mentors")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMentors(CancellationToken cancellationToken)
         => ToActionResult(await service.GetMentorsAsync(cancellationToken));

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
        => ToActionResult(await service.GetPlansAsync(cancellationToken));

    [HttpGet("availability")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? mentorId,
        CancellationToken cancellationToken)
        => ToActionResult(await service.GetAvailableSlotsAsync(from, to, mentorId, cancellationToken));

    [HttpPost("purchase")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseMentorPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FrontendOrigin))
            request.FrontendOrigin = RequestOrigin();
        return ToActionResult(await service.PurchaseAsync(request, cancellationToken));
    }

    private string? RequestOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin)) return origin;
        return Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            ? referer.GetLeftPart(UriPartial.Authority)
            : null;
    }

    [HttpGet("purchases/{subscriptionId:int}")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetPurchaseStatus(int subscriptionId, CancellationToken cancellationToken)
        => ToActionResult(await service.GetPurchaseStatusAsync(subscriptionId, cancellationToken));

    [HttpGet("purchases")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> GetMyPurchases(CancellationToken ct)
        => ToActionResult(await service.GetMyPurchasesAsync(ct));

    [HttpPost("purchases/{subscriptionId:int}/sync")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> SyncPurchase(int subscriptionId, CancellationToken cancellationToken)
        => ToActionResult(await service.SyncPurchaseAsync(subscriptionId, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
