using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.JapaneseDictionary;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/japanese-dictionary")]
public class JapaneseDictionaryController(IJapaneseDictionaryService dictionaryService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string readingKana,
        CancellationToken cancellationToken)
        => Ok(await dictionaryService.SearchAsync(readingKana, cancellationToken));

    [HttpPost("personal")]
    public async Task<IActionResult> AddPersonal(
        [FromBody] CreatePersonalDictionaryEntryRequest request,
        CancellationToken cancellationToken)
        => Ok(await dictionaryService.AddPersonalAsync(request, cancellationToken));
}
