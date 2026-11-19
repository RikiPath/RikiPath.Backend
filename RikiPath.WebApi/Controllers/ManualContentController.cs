using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.Services;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/content-author/manual")]
[Authorize(Roles = "ContentAuthor,Admin")]
public sealed class ManualContentController(ManualContentService service, IUnitOfWork uow) : ControllerBase
{
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var levels = await uow.CertificateLevels.Query().Where(x => !x.IsDeleted
            && !x.CertificateType.IsDeleted && x.CertificateType.IsActive)
            .OrderBy(x => x.CertificateType.Name).ThenBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.Code, Certificate = x.CertificateType.Name }).ToListAsync(ct);
        var skills = await uow.LanguageSkills.Query().Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var sections = await uow.CertificateSections.Query().Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder).Select(x => new { x.Code, x.Name, x.CertificateLevelId }).ToListAsync(ct);
        var topics = await uow.Topics.Query().Where(x => !x.IsDeleted)
            .OrderBy(x => x.Title).ThenBy(x => x.Id).Select(x => new { x.Id, x.Title }).ToListAsync(ct);
        return Ok(new { levels, skills, sections, topics, jlptQuestionFormats = JlptMockQuestionFormats.All });
    }

    [HttpGet("lessons/{levelId:int}")]
    public async Task<IActionResult> Lessons(int levelId, CancellationToken ct) => Ok(
        await uow.Lessons.Query().Where(x => !x.IsDeleted && x.CertificateLevelId == levelId)
            .OrderBy(x => x.Title).Select(x => new { x.Id, x.Title }).ToListAsync(ct));

    [HttpGet("items/{type}")]
    public async Task<IActionResult> List(ImportEntityType type, [FromQuery] ContentApprovalStatus? status, CancellationToken ct)
        => Ok(await service.ListAsync(type, status, ct));

    [HttpGet("import-batches")]
    public async Task<IActionResult> ImportBatches(CancellationToken ct) => Ok(await service.GetImportBatchesAsync(ct));

    [HttpGet("import-batches/{batchId:guid}")]
    public async Task<IActionResult> ImportBatch(Guid batchId, CancellationToken ct)
    {
        var items = await service.ListAsync(null, null, ct, batchId);
        return items.Count == 0 ? NotFound() : Ok(items);
    }

    [HttpPost("import-batches/{batchId:guid}/submit")]
    public async Task<IActionResult> SubmitImportBatch(Guid batchId, CancellationToken ct)
    {
        var result = await service.SubmitBatchAsync(batchId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("items/detail/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("items/{type}")]
    [HttpPut("items/{type}/{id:int}")]
    public async Task<IActionResult> Save(ImportEntityType type, int? id, [FromBody] JsonElement input, CancellationToken ct)
    {
        var result = await service.SaveAsync(type, id, input, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("items/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        var result = await service.SubmitAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}
