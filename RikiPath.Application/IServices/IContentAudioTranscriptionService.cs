using System.Text.Json;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.IServices;

public interface IContentAudioTranscriptionService
{
    Task<ListeningAudioUploadResponse> UploadAsync(
        Stream audio, string fileName, int authorId, CancellationToken ct);
    Task<ContentAudioTranscriptionResponse?> QueueAsync(string audioUrl, int? authorId, CancellationToken ct);
    Task QueuePayloadAsync(JsonElement payload, int authorId, CancellationToken ct);
    Task<ContentAudioTranscriptionResponse?> GetAsync(string audioUrl, CancellationToken ct);
    Task<IReadOnlyList<ContentAudioTranscriptionResponse>> ListMineAsync(int authorId, CancellationToken ct);
    Task<ApiResponse<ContentAudioTranscriptionResponse>> RetryAsync(string audioUrl, CancellationToken ct);
    Task<ContentAudioTranscriptionResponse?> GetForGradingAsync(
        string audioUrl, JsonElement? interactionData, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
