using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IClients
{
    public interface ISpeechRecognitionClient
    {
        Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken cancellationToken);
        Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken);

        Task<string> IssueTokenAsync(CancellationToken cancellationToken = default);
    }
}
