using Microsoft.AspNetCore.Http;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IServices
{
    public interface ISpeechToTextService
    {
        Task<string> ConvertAudioToTextAsync(IFormFile audioFile);
        Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken);
    }
}
