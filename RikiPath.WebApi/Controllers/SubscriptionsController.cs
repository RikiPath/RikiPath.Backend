using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize(Roles = "Learner")]
public sealed class SubscriptionsController(SubscriptionAccessService subscriptions) : ControllerBase
{
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<StudyPlanOffer>>.Success(await subscriptions.GetPlansAsync(ct)));

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => Ok(ApiResponse<StudyAccessResponse>.Success(await subscriptions.GetAccessAsync(ct)));

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] StudyPlanPurchase request, CancellationToken ct)
    {
        var result = await subscriptions.PurchaseAsync(request.SubscriptionPlanId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("purchases/{id:int}")]
    public async Task<IActionResult> Status(int id, CancellationToken ct)
    {
        var result = await subscriptions.GetPurchaseStatusAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}

public sealed record StudyPlanPurchase(int SubscriptionPlanId);
