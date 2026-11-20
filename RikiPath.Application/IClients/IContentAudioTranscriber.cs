using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IClients;

public interface IContentAudioTranscriber
{
    bool IsConfigured { get; }
    int TimeoutSeconds { get; }
    Task<SpeechToTextResult> TranscribeListeningAsync(string audioUrl, CancellationToken cancellationToken);
}
