using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IClients
{
    public interface IAzureSpeechClient
    {
        Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken);

        Task<string> IssueTokenAsync(CancellationToken cancellationToken = default);
    }
}
