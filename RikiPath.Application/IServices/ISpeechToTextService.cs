using Microsoft.AspNetCore.Http;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IServices
{
    public interface ISpeechToTextService
    {
        Task<string> ConvertAudioToTextAsync(IFormFile audioFile, CancellationToken cancellationToken = default);
        async Task<RecordedSpeechResult> ConvertAndSaveAudioAsync(IFormFile audioFile, CancellationToken cancellationToken = default)
            => new(await ConvertAudioToTextAsync(audioFile, cancellationToken), null);
        Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken);
        Task<ApiResponse<SpeechStreamingSessionResponse>> CreateStreamingSessionAsync(CancellationToken cancellationToken);
    }
}
