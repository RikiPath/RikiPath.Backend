using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.Highlights;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Learner")]
public class HighlightsController(ILearnerHighlightService highlightService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHighlights(
        [FromQuery] string? targetType = null,
        [FromQuery] int? targetId = null,
        [FromQuery] string? targetKey = null,
        CancellationToken cancellationToken = default)
    {
        var response = await highlightService.GetHighlightsAsync(
            GetCurrentUserId(), targetType, targetId, targetKey, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHighlight(
        [FromBody] CreateHighlightRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await highlightService.CreateHighlightAsync(
            GetCurrentUserId(), request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{highlightId:int}")]
    public async Task<IActionResult> UpdateHighlight(
        int highlightId,
        [FromBody] UpdateHighlightRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await highlightService.UpdateHighlightAsync(
            GetCurrentUserId(), highlightId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{highlightId:int}")]
    public async Task<IActionResult> DeleteHighlight(
        int highlightId,
        CancellationToken cancellationToken = default)
    {
        var response = await highlightService.DeleteHighlightAsync(
            GetCurrentUserId(), highlightId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{highlightId:int}/to-srs")]
    public async Task<IActionResult> PromoteToSrs(
        int highlightId,
        [FromBody] HighlightToSrsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var response = await highlightService.PromoteToSrsAsync(
            GetCurrentUserId(), highlightId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
