using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/japanese-dictionary")]
public sealed class JapaneseDictionaryController(IJapaneseDictionaryService dictionary) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? readingKana, CancellationToken cancellationToken)
    {
        var response = await dictionary.SearchAsync(readingKana, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
