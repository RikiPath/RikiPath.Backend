using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/admin/teaching-proposals")]
[Authorize(Roles = "Admin")]
public sealed class AdminTeachingProposalController(IMentorTeachingProposalService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int? mentorId,
        [FromQuery] int? certificateLevelId,
        CancellationToken ct)
        => ToActionResult(await service.GetAdminProposalsAsync(status, mentorId, certificateLevelId, ct));

    [HttpGet("approved-for-cohort")]
    public async Task<IActionResult> GetApprovedForCohort(
        [FromQuery] int? mentorId,
        [FromQuery] int? certificateLevelId,
        CancellationToken ct)
        => ToActionResult(await service.GetApprovedProposalsForCohortAsync(mentorId, certificateLevelId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => ToActionResult(await service.GetAdminProposalByIdAsync(id, ct));

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewTeachingProposalRequest request, CancellationToken ct)
        => ToActionResult(await service.ReviewProposalAsync(id, request, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
