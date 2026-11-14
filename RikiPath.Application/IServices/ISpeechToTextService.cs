using Microsoft.AspNetCore.Http;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IServices
{
    public interface ISpeechToTextService
    {
        Task<string> ConvertAudioToTextAsync(IFormFile audioFile, CancellationToken cancellationToken = default);
        Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken);
    }
}
