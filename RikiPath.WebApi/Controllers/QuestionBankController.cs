using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/content-author/question-bank")]
[Authorize(Roles = "ContentAuthor,Admin")]
public sealed class QuestionBankController(
    IQuestionBankService questionBankService,
    IQuestionAiAssistantService aiAssistantService,
    IClaimService claimService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int? certificateLevelId,
        [FromQuery] int? languageSkillId,
        [FromQuery] string? questionType,
        [FromQuery] string? templateKind,
        [FromQuery] ContentStatus? publishStatus,
        [FromQuery] string? sourceType,
        [FromQuery] int? passageId,
        [FromQuery] bool? hasMedia,
        [FromQuery] bool onlyMine = false,
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new QuestionBankQueryFilter
        {
            CertificateLevelId = certificateLevelId,
            LanguageSkillId = languageSkillId,
            QuestionType = questionType,
            TemplateKind = templateKind,
            PublishStatus = publishStatus,
            SourceType = sourceType,
            PassageId = passageId,
            HasMedia = hasMedia,
            OnlyMine = onlyMine,
            IncludeArchived = includeArchived,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize,
        };
        var user = claimService.GetUserClaim();
        var res = await questionBankService.SearchAsync(filter, user.Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct)
    {
        var user = claimService.GetUserClaim();
        var res = await questionBankService.GetDetailAsync(id, user.Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft([FromBody] CanonicalQuestionDto request, CancellationToken ct)
    {
        var res = await questionBankService.SaveDraftAsync(request, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("{id:int}/new-version")]
    public async Task<IActionResult> CreateNewVersion(
        int id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CanonicalQuestionDto? updates = null,
        CancellationToken ct = default)
    {
        var res = await questionBankService.CreateNewVersionAsync(id, updates, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        var res = await questionBankService.SubmitQuestionAsync(id, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken ct)
    {
        var res = await questionBankService.DeleteDraftAsync(id, claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("check-duplicates")]
    public async Task<IActionResult> CheckDuplicates([FromBody] CanonicalQuestionDto candidate, [FromQuery] int? excludeId, CancellationToken ct)
    {
        var res = await questionBankService.CheckDuplicatesAsync(candidate, excludeId, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("passages")]
    public async Task<IActionResult> ListPassages(
        [FromQuery] int? levelId,
        [FromQuery] int? skillId,
        [FromQuery] ContentStatus? status,
        [FromQuery] string? keyword,
        CancellationToken ct)
    {
        var res = await questionBankService.ListPassagesAsync(
            levelId, skillId, status, keyword, claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("passages/{passageId:int}")]
    public async Task<IActionResult> GetPassageGroup(int passageId, CancellationToken ct)
    {
        var res = await questionBankService.GetPassageGroupDetailAsync(
            passageId, claimService.GetUserClaim().Id, User.IsInRole("Admin"), ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("passages/draft")]
    public async Task<IActionResult> SavePassageGroupDraft([FromBody] CanonicalPassageWithQuestionsDto request, CancellationToken ct)
    {
        var res = await questionBankService.SavePassageGroupDraftAsync(request, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("passages/{passageId:int}/submit")]
    public async Task<IActionResult> SubmitPassageGroup(int passageId, CancellationToken ct)
    {
        var res = await questionBankService.SubmitPassageGroupAsync(passageId, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("ai/assist")]
    public async Task<IActionResult> AiAssist([FromBody] QuestionAiAssistRequest request, CancellationToken ct)
    {
        var res = await aiAssistantService.AssistAsync(request, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }
}
