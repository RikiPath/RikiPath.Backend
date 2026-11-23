using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/content-author/exams")]
[Authorize(Roles = "ContentAuthor,Admin")]
public sealed class ExamBuilderController(
    IExamBuilderService examBuilderService,
    IClaimService claimService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ExamKind? examKind,
        [FromQuery] int? levelId,
        [FromQuery] ContentStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var res = await examBuilderService.ListAuthorExamsAsync(
            examKind, levelId, status, keyword, page, pageSize,
            claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("{examId:int}")]
    public async Task<IActionResult> GetDetail(int examId, CancellationToken ct)
    {
        var res = await examBuilderService.GetDetailAsync(
            examId, claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft([FromBody] ExamBuilderDto request, CancellationToken ct)
    {
        var res = await examBuilderService.SaveDraftAsync(request, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("{examId:int}/new-version")]
    public async Task<IActionResult> CreateNewVersion(int examId, CancellationToken ct)
    {
        var res = await examBuilderService.CreateNewVersionAsync(examId, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("{examId:int}/validate")]
    public async Task<IActionResult> ValidateBlueprint(int examId, CancellationToken ct)
    {
        var res = await examBuilderService.ValidateExamAsync(examId, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("{examId:int}/submit")]
    public async Task<IActionResult> Submit(int examId, CancellationToken ct)
    {
        var res = await examBuilderService.SubmitExamAsync(examId, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpDelete("{examId:int}")]
    public async Task<IActionResult> DeleteDraft(int examId, CancellationToken ct)
    {
        var res = await examBuilderService.DeleteDraftExamAsync(
            examId, claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }
}
