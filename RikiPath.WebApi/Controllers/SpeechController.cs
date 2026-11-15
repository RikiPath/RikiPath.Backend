using Microsoft.AspNetCore.Authorization;
using RikiPath.Application.Services;
using RikiPath.Application.Responses;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Practice;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SpeechController(ISpeechToTextService speechService, SubscriptionAccessService subscriptions,
    IPracticeSubmissionService practice) : ControllerBase
{
    /// <summary>Issues a temporary Azure token for compatible streaming clients.</summary>
    [HttpGet("get-token")]
    public async Task<IActionResult> GetToken(CancellationToken cancellationToken)
    {
        var result = await speechService.GetTokenAsync(cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Transcribe Japanese audio through OpenAI. Multipart field: audioFile.</summary>
    [HttpPost("transcribe")]
    [Authorize(Roles = "Learner,Mentor")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    public async Task<IActionResult> Transcribe(IFormFile audioFile, CancellationToken cancellationToken)
    {
        try
        {
            var result = await subscriptions.UseAiAsync("speaking", async () =>
            {
                var text = await speechService.ConvertAudioToTextAsync(audioFile, cancellationToken);
                return ApiResponse<string>.Success(text);
            }, cancellationToken);
            return result.IsSuccess ? Ok(new { text = result.Result }) : StatusCode(result.StatusCode, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AiQuotaExceededException ex) { return StatusCode(429, new { message = ex.Message }); }
        catch (AiServiceException ex) { return StatusCode(502, new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "Không thể kết nối OpenAI." }); }
    }

    [HttpPost("grade")]
    [Authorize(Roles = "Learner,Mentor")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    public async Task<IActionResult> Grade(IFormFile audioFile, [FromForm] int certificateLevelId,
        [FromForm] string? topic, CancellationToken cancellationToken)
    {
        if (certificateLevelId <= 0)
            return BadRequest(new { message = "Vui lòng chọn cấp độ JLPT." });

        try
        {
            var result = await subscriptions.UseAiAsync("speaking", async () =>
            {
                var text = await speechService.ConvertAudioToTextAsync(audioFile, cancellationToken);
                return await practice.SubmitAsync(new SubmitPracticeRequest
                {
                    Type = SubmissionType.Speaking,
                    CertificateLevelId = certificateLevelId,
                    TextContent = text,
                    Topic = topic,
                }, cancellationToken);
            }, cancellationToken, value => value?.Grading is not null);
            return StatusCode(result.StatusCode, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AiQuotaExceededException ex) { return StatusCode(429, new { message = ex.Message }); }
        catch (AiServiceException ex) { return StatusCode(502, new { message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "Không thể kết nối OpenAI." }); }
    }

    /// <summary>Creates a short-lived Japanese PCM streaming session for Amazon Transcribe.</summary>
    [Authorize(Roles = "Learner")]
    [HttpPost("streaming-session")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("speech-session")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CreateStreamingSession(CancellationToken cancellationToken)
    {
        var result = await speechService.CreateStreamingSessionAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

}
