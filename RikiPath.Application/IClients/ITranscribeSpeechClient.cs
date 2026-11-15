using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IClients;

public interface ITranscribeSpeechClient
{
    Task<SpeechStreamingSessionResponse> CreateStreamingSessionAsync(CancellationToken cancellationToken);
    Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken);
}
