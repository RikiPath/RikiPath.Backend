using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.WebApi.Services;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/admin/teaching-proposals")]
[Authorize(Roles = "Admin")]
public sealed class AdminTeachingProposalController(
    IMentorTeachingProposalService service,
    IFileStorageService fileStorageService,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int? mentorId,
        [FromQuery] int? certificateLevelId,
        CancellationToken ct)
        => ToActionResult(await service.GetAdminProposalsAsync(status, mentorId, certificateLevelId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => ToActionResult(await service.GetAdminProposalByIdAsync(id, ct));

    [HttpGet("{id:int}/attachments/{index:int}")]
    public async Task<IActionResult> GetAttachment(int id, int index, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        var meta = await service.GetAuthorizedAttachmentAsync(id, index, isAdmin: true, ct);
        if (!meta.IsSuccess || meta.Result is null)
            return ToActionResult(meta);

        return await ProposalAttachmentStreamHelper.StreamAttachmentAsync(
            this, fileStorageService, httpClientFactory, meta.Result, download, ct);
    }

    [HttpGet("{id:int}/attachments/{index:int}/preview")]
    public async Task<IActionResult> PreviewAttachment(int id, int index, CancellationToken ct = default)
    {
        var meta = await service.GetAuthorizedAttachmentAsync(id, index, isAdmin: true, ct);
        if (!meta.IsSuccess || meta.Result is null)
            return ToActionResult(meta);

        return await ProposalAttachmentStreamHelper.PreviewAttachmentAsync(
            this, fileStorageService, httpClientFactory, meta.Result, ct);
    }

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewTeachingProposalRequest request, CancellationToken ct)
        => ToActionResult(await service.ReviewProposalAsync(id, request, ct));

    [HttpPost("bulk-approve/validate")]
    public async Task<IActionResult> PrevalidateBulkApprove([FromBody] BulkApproveTeachingProposalsRequest request, CancellationToken ct)
        => ToActionResult(await service.PrevalidateBulkApproveAsync(request, ct));

    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApproveTeachingProposalsRequest request, CancellationToken ct)
        => ToActionResult(await service.BulkApproveProposalsAsync(request, ct));

    [HttpPost("repair-invalid-schedules")]
    public async Task<IActionResult> RepairInvalidNeedsRevisionSchedules(CancellationToken ct)
        => ToActionResult(await service.RepairInvalidNeedsRevisionSchedulesAsync(ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
