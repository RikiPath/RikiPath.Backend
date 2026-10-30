using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

/// <summary>QA-only payment completion; the service independently enforces environment and local database gates.</summary>
[ApiController]
[Route("api/admin/qa-payments")]
[Authorize(Roles = "Admin")]
public sealed class AdminQaPaymentController(IMentorMeetingService mentorMeetingService) : ControllerBase
{
    [HttpPost("{subscriptionId:int}/complete")]
    public async Task<IActionResult> Complete(int subscriptionId, CancellationToken cancellationToken)
    {
        if (subscriptionId <= 0) return BadRequest();
        var response = await mentorMeetingService.CompleteQaPaymentAsync(subscriptionId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
