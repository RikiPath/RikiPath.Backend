using Microsoft.AspNetCore.Authorization;
using RikiPath.Application.Services;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/learner-essays")]
[Authorize(Roles = "Learner")]
public class LearnerEssayController(ILearnerEssayService essayService, SubscriptionAccessService subscriptions) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        [FromForm] EssayImageRequest request,
        CancellationToken cancellationToken)
        => (await subscriptions.GetAccessAsync(cancellationToken)).Writing
            ? Ok(await essayService.ScanAsync(request.Image, cancellationToken))
            : StatusCode(403, new { errorCode = "WRITING_PACKAGE_REQUIRED", errorMessage = "Bạn cần gói luyện viết." });

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await essayService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => Ok(await essayService.GetByIdAsync(id, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateEssayRequest request,
        CancellationToken cancellationToken)
        => Ok(await essayService.UpdateAsync(id, request.ContentText, cancellationToken));

    [HttpPost("{id:int}/rescan")]
    public async Task<IActionResult> Rescan(
        int id,
        [FromForm] EssayImageRequest request,
        CancellationToken cancellationToken)
        => (await subscriptions.GetAccessAsync(cancellationToken)).Writing
            ? Ok(await essayService.RescanAsync(id, request.Image, cancellationToken))
            : StatusCode(403, new { errorCode = "WRITING_PACKAGE_REQUIRED", errorMessage = "Bạn cần gói luyện viết." });

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => Ok(await essayService.DeleteAsync(id, cancellationToken));
}

public record UpdateEssayRequest(string ContentText);

public class EssayImageRequest
{
    public IFormFile Image { get; set; } = null!;
}
