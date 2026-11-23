using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.SrsTemplates;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/admin/content-publish")]
[Authorize(Roles = "Admin")]
public class ContentPublishController(IContentPublishService publishService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] string? entityType = "Vocabulary",
        [FromQuery] ContentStatus? status = null,
        [FromQuery] string? jlptLevel = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await publishService.GetCatalogAsync(entityType, status, jlptLevel, search, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> TogglePublish(
        [FromBody] TogglePublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var response = await publishService.TogglePublishStateAsync(adminId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
