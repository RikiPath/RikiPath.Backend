using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RikiPath.Application.DTOs.Dictionary;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Learner,ContentAuthor,Admin")]
public class DictionaryController(IDictionaryLookupService dictionaryService) : ControllerBase
{
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] string? q = null,
        [FromQuery] string? level = null,
        CancellationToken cancellationToken = default)
    {
        var response = await dictionaryService.LookupAsync(
            TryGetCurrentUserId(), q, level, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("translate")]
    [EnableRateLimiting("dictionary-translate")]
    public async Task<IActionResult> Translate(
        [FromBody] TranslateSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null) return Unauthorized();

        var response = await dictionaryService.TranslateAsync(
            userId.Value, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    private int? TryGetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
