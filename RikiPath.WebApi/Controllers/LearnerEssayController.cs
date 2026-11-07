using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/learner-essays")]
[Authorize(Roles = "Learner")]
public class LearnerEssayController(ILearnerEssayService essayService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] SaveEssayDraftRequest request, CancellationToken cancellationToken)
        => Respond(await essayService.CreateAsync(request.Image, request.ContentText, request.OriginalOcrText, cancellationToken));

    [HttpPut("{id:int}/draft")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SaveDraft(int id, [FromForm] SaveEssayDraftRequest request, CancellationToken cancellationToken)
        => Respond(await essayService.SaveDraftAsync(id, request.Image, request.ContentText, request.OriginalOcrText, cancellationToken));

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        [FromForm] EssayImageRequest request,
        CancellationToken cancellationToken)
        => Respond(await essayService.ScanAsync(request.Image, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Respond(await essayService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => Respond(await essayService.GetByIdAsync(id, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateEssayRequest request,
        CancellationToken cancellationToken)
        => Respond(await essayService.UpdateAsync(id, request.ContentText, cancellationToken));

    [HttpPost("{id:int}/rescan")]
    public async Task<IActionResult> Rescan(
        int id,
        [FromForm] EssayImageRequest request,
        CancellationToken cancellationToken)
        => Respond(await essayService.RescanAsync(id, request.Image, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => Respond(await essayService.DeleteAsync(id, cancellationToken));

    private IActionResult Respond<T>(ApiResponse<T> result) => StatusCode(result.StatusCode, result);
}

public class SaveEssayDraftRequest
{
    public IFormFile? Image { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public string? OriginalOcrText { get; set; }
}

public record UpdateEssayRequest(string ContentText);

public class EssayImageRequest
{
    public IFormFile Image { get; set; } = null!;
}
