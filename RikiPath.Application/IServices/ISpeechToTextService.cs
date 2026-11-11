using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IServices
{
    public interface ISpeechToTextService
    {
        Task<ApiResponse<SpeechTokenResponse>> GetTokenAsync(CancellationToken cancellationToken);
        Task<ApiResponse<SpeechStreamingSessionResponse>> CreateStreamingSessionAsync(CancellationToken cancellationToken);
    }
}
