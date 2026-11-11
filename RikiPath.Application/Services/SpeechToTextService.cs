using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Domain;
using System;
using System.Net;

namespace RikiPath.Application.Services
{
    public class SpeechToTextService(IAzureSpeechClient azureSpeechClient, AppSettings appSettings, ILogger<SpeechToTextService> logger, ITranscribeSpeechClient transcribeClient) : ISpeechToTextService
    {
        private const int TokenLifetimeSeconds = 600;

        public async Task<ApiResponse<SpeechStreamingSessionResponse>> CreateStreamingSessionAsync(CancellationToken cancellationToken)
        {
            try
            {
                return ApiResponse<SpeechStreamingSessionResponse>.Success(
                    await transcribeClient.CreateStreamingSessionAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                // Credential errors can contain provider details. Do not expose keys or signed URLs.
                logger.LogWarning("Could not create an Amazon Transcribe streaming session. Check AWS IAM credentials and region.");
                return ApiResponse<SpeechStreamingSessionResponse>.Fail(
                    "Không thể mở phiên nhận dạng. Hãy kiểm tra cấu hình AWS và quyền Amazon Transcribe.", HttpStatusCode.ServiceUnavailable);
            }
        }

        public async Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                var token = await azureSpeechClient.IssueTokenAsync(cancellationToken);
                var settings = appSettings.AzureSpeechSettings;

                return ApiResponse<SpeechTokenResponse>.Success(new SpeechTokenResponse
                {
                    Token = token,
                    Region = settings.Region.Trim(),
                    Language = settings.Language,
                    ExpiresInSeconds = TokenLifetimeSeconds
                }, message: "Lấy token Azure Speech thành công.");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Cấu hình AzureSpeech không hợp lệ.");
                return ApiResponse<SpeechTokenResponse>.Fail(message: "Hệ thống chưa cấu hình dịch vụ nhận diện giọng nói.",
                    HttpStatusCode.InternalServerError);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Azure Speech từ chối cấp token. Status: {Status}", ex.StatusCode);
                return ApiResponse<SpeechTokenResponse>.Fail(message: "Không thể lấy token từ Azure Speech.",
                    HttpStatusCode.BadGateway);
            }
        }
    }
}
