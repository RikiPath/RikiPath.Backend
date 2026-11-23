using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/mentor/teaching-proposals")]
[Authorize(Roles = "Mentor")]
public sealed class MentorTeachingProposalController(IMentorTeachingProposalService service) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> GetContext(CancellationToken ct)
        => ToActionResult(await service.GetMentorContextAsync(ct));

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] string? status, CancellationToken ct)
        => ToActionResult(await service.GetMyProposalsAsync(status, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => ToActionResult(await service.GetMyProposalByIdAsync(id, ct));

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft([FromBody] SaveTeachingProposalDraftRequest request, CancellationToken ct)
        => ToActionResult(await service.CreateDraftAsync(request, ct));

    [HttpPut("{id:int}/draft")]
    public async Task<IActionResult> UpdateDraft(int id, [FromBody] SaveTeachingProposalDraftRequest request, CancellationToken ct)
        => ToActionResult(await service.UpdateDraftAsync(id, request, ct));

    [HttpDelete("{id:int}/draft")]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken ct)
        => ToActionResult(await service.DeleteDraftAsync(id, ct));

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateTeachingProposalRequest request, CancellationToken ct)
        => ToActionResult(await service.ValidateDraftAsync(request, ct));

    [HttpPost("{id:int}/submit")]
    [EnableRateLimiting("mentor-availability-submit")]
    public async Task<IActionResult> Submit(int id, [FromBody] SubmitTeachingProposalRequest request, CancellationToken ct)
        => ToActionResult(await service.SubmitProposalAsync(id, request, ct));

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, [FromBody] WithdrawTeachingProposalRequest? request, CancellationToken ct)
        => ToActionResult(await service.WithdrawProposalAsync(id, request, ct));

    [HttpPost("{id:int}/clone-draft")]
    public async Task<IActionResult> CloneToDraft(int id, CancellationToken ct)
        => ToActionResult(await service.ReopenForRevisionAsync(id, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
