using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/audio")]
public sealed class AudioController(IAwsAudioFileStorageService audio) : ControllerBase
{
    [HttpGet("library/{**objectKey}")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LibraryAudio(string objectKey, CancellationToken ct)
    {
        try
        {
            if (!audio.TryGetContentReference(audio.S3Reference(objectKey), out var reference)) return NotFound();
            return Redirect(await audio.CreateSignedReadUrlAsync(reference, ct));
        }
        catch (ArgumentException) { return NotFound(); }
        catch (InvalidOperationException) { return StatusCode(503); }
    }

    [HttpGet("content/{ownerId:int}/{fileName}")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ContentAudio(int ownerId, string fileName, CancellationToken ct)
    {
        try
        {
            var reference = audio.ContentReference(ownerId, fileName);
            return Redirect(await audio.CreateSignedReadUrlAsync(reference, ct));
        }
        catch (ArgumentException) { return NotFound(); }
        catch (InvalidOperationException)
        {
            return StatusCode(503, new { message = "Kho audio AWS chưa được cấu hình." });
        }
    }
}
