using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "ContentAuthor,Admin")]
[Route("api/content-import/listening-audio/transcript")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContentAudioTranscriptionController(
    IContentAudioTranscriptionService transcriptions, IClaimService claims) : ControllerBase
{
    public sealed record AudioRequest([property: Required, StringLength(2048)] string AudioUrl);

    [HttpGet("/api/content-import/listening-audio/uploads")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ContentAudioTranscriptionResponse>>.Success(
            await transcriptions.ListMineAsync(claims.GetUserClaim().Id, ct)));

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery, Required, StringLength(2048)] string audioUrl, CancellationToken ct)
        => Ok(ApiResponse<ContentAudioTranscriptionResponse?>.Success(await transcriptions.GetAsync(audioUrl, ct)));

    [HttpPost]
    public async Task<IActionResult> QueueOrRetry(AudioRequest request, CancellationToken ct)
    {
        await transcriptions.QueueAsync(request.AudioUrl, claims.GetUserClaim().Id, ct);
        var response = await transcriptions.RetryAsync(request.AudioUrl, ct);
        return StatusCode(response.StatusCode, response);
    }
}
