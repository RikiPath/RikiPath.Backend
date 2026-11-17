using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/mentor-group-policies")]
public sealed class AdminMentorGroupPolicyController(
    IMentorGroupPolicyService policies,
    IClaimService claims) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var policy = await policies.GetEffectiveAsync(DateTime.UtcNow, cancellationToken);
        return policy is null
            ? NotFound(ApiResponse<MentorGroupPolicy>.NotFound("Chưa có chính sách buổi học áp dụng."))
            : Ok(ApiResponse<MentorGroupPolicy>.Success(policy));
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MentorGroupPolicy>>.Success(
            await policies.GetHistoryAsync(cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreateMentorGroupPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await policies.CreateVersionAsync(
            request, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
