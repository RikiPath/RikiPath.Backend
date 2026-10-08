using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Services
{
    public class SpeechToTextService(IAzureSpeechClient azureSpeechClient, AppSettings appSettings, ILogger<SpeechToTextService> logger) : ISpeechToTextService
    {
        private const int TokenLifetimeSeconds = 600;

        public Task<string> ConvertAudioToTextAsync(IFormFile audioFile)
        {
            throw new NotImplementedException();
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