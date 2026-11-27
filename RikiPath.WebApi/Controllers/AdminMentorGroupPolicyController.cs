using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
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

    [HttpGet("levels")]
    public async Task<IActionResult> GetSupportedLevels(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SupportedPolicyLevelDto>>.Success(
            await policies.GetSupportedLevelsAsync(cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreateMentorGroupPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await policies.CreateVersionAsync(
            request, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateVersion(
        int id,
        [FromBody] UpdateMentorGroupPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await policies.UpdateVersionAsync(
            id, request, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPatch("{id:int}/sections/{sectionKey}")]
    public async Task<IActionResult> UpdateSection(
        int id,
        string sectionKey,
        [FromBody] UpdateMentorGroupPolicySectionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await policies.UpdateSectionAsync(
            id, sectionKey, request, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> ActivateVersion(
        int id,
        CancellationToken cancellationToken)
    {
        var response = await policies.ActivateVersionAsync(
            id, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVersion(
        int id,
        CancellationToken cancellationToken)
    {
        var response = await policies.DeleteVersionAsync(
            id, claims.GetUserClaim().Id, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
