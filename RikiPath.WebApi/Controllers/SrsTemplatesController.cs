using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.SrsTemplates;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/srs-templates")]
[Authorize]
public class SrsTemplatesController(ISrsCardTemplateService templateService) : ControllerBase
{
    [HttpGet("published")]
    [Authorize(Roles = "Learner,ContentAuthor,Admin")]
    public async Task<IActionResult> GetPublished(
        [FromQuery] string? jlptLevel = null,
        [FromQuery] string? cardType = null,
        [FromQuery] int? lessonId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.GetPublishedForLearnerAsync(
            GetCurrentUserId(), jlptLevel, cardType, lessonId, search, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> GetMine(
        [FromQuery] ContentStatus? status = null,
        [FromQuery] string? cardType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.GetMyTemplatesAsync(
            GetCurrentUserId(), status, cardType, search, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetForAdmin(
        [FromQuery] ContentStatus? status = null,
        [FromQuery] string? jlptLevel = null,
        [FromQuery] string? cardType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.GetForAdminAsync(
            status, jlptLevel, cardType, search, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken = default)
    {
        var response = await templateService.GetForAdminAsync(
            ContentStatus.PendingReview, null, null, null, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.CreateDraftAsync(
            GetCurrentUserId(), request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{templateId:int}")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> UpdateDraft(
        int templateId,
        [FromBody] SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.UpdateDraftAsync(
            GetCurrentUserId(), User.IsInRole("Admin"), templateId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{templateId:int}/submit")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> Submit(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.SubmitForReviewAsync(
            GetCurrentUserId(), User.IsInRole("Admin"), templateId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{templateId:int}")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> DeleteDraft(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.DeleteDraftAsync(
            GetCurrentUserId(), User.IsInRole("Admin"), templateId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{templateId:int}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(
        int templateId,
        [FromBody] ReviewSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.ReviewTemplateAsync(
            GetCurrentUserId(), templateId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{templateId:int}/toggle-publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TogglePublish(
        int templateId,
        [FromBody] TogglePublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await templateService.TogglePublishAsync(
            GetCurrentUserId(), templateId, request.Publish, request.Reason, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
