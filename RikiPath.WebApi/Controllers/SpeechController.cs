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

    /// <summary>Save audio to S3 and transcribe Japanese with the dedicated AwsAudio configuration.</summary>
    [HttpPost("transcribe")]
    [Authorize(Roles = "Learner,Mentor")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    public async Task<IActionResult> Transcribe(IFormFile audioFile, CancellationToken cancellationToken)
    {
        try
        {
            var result = await subscriptions.UseAiAsync("speaking", async () =>
            {
                var audio = await speechService.ConvertAndSaveAudioAsync(audioFile, cancellationToken);
                return ApiResponse<RikiPath.Application.Responses.SpeechToText.RecordedSpeechResult>.Success(audio);
            }, cancellationToken);
            return result.IsSuccess ? Ok(new { text = result.Result?.Transcript, audioUrl = result.Result?.AudioUrl })
                : StatusCode(result.StatusCode, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AiQuotaExceededException ex) { return StatusCode(429, new { message = ex.Message }); }
        catch (AiServiceException ex) { return StatusCode(502, new { message = ex.Message }); }
        catch (InvalidOperationException) { return StatusCode(503, new { message = "Dịch vụ audio AWS chưa sẵn sàng. Kiểm tra cấu hình AwsAudio." }); }
        catch (Amazon.Runtime.AmazonServiceException) { return StatusCode(502, new { message = "Không thể lưu hoặc nhận dạng audio trên AWS. Kiểm tra quyền S3 và Transcribe." }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "Không thể kết nối dịch vụ nhận dạng audio." }); }
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
                var audio = await speechService.ConvertAndSaveAudioAsync(audioFile, cancellationToken);
                return await practice.SubmitAsync(new SubmitPracticeRequest
                {
                    Type = SubmissionType.Speaking,
                    CertificateLevelId = certificateLevelId,
                    TextContent = audio.Transcript,
                    AudioUrl = audio.AudioUrl,
                    Topic = topic,
                }, cancellationToken);
            }, cancellationToken, value => value?.Grading is not null);
            return StatusCode(result.StatusCode, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AiQuotaExceededException ex) { return StatusCode(429, new { message = ex.Message }); }
        catch (AiServiceException ex) { return StatusCode(502, new { message = ex.Message }); }
        catch (InvalidOperationException) { return StatusCode(503, new { message = "Dịch vụ audio AWS chưa sẵn sàng. Kiểm tra cấu hình AwsAudio." }); }
        catch (Amazon.Runtime.AmazonServiceException) { return StatusCode(502, new { message = "Không thể lưu hoặc nhận dạng audio trên AWS. Kiểm tra quyền S3 và Transcribe." }); }
        catch (HttpRequestException) { return StatusCode(502, new { message = "Không thể kết nối dịch vụ chấm bài." }); }
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
