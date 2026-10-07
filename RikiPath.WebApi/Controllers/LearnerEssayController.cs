using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/learner-essays")]
public class LearnerEssayController(ILearnerEssayService essayService) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        [FromForm] EssayImageRequest request,
        CancellationToken cancellationToken)
        => Ok(await essayService.ScanAsync(request.Image, cancellationToken));

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
        => Ok(await essayService.RescanAsync(id, request.Image, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => Ok(await essayService.DeleteAsync(id, cancellationToken));
}

public record UpdateEssayRequest(string ContentText);

public class EssayImageRequest
{
    public IFormFile Image { get; set; } = null!;
}
