using RikiPath.Application.Responses.SpeechToText;

namespace RikiPath.Application.IClients
{
    // Implementation thực tế (gọi Azure Speech Service REST API) đặt bên
    // RikiPath.Infrastructure.Clients, tương tự AiLearningPathClient/PayOsClient đã có.
    public interface ISpeechToTextClient
    {
        // audioUrl: URL công khai của file audio đã upload lên Supabase Storage.
        // Client tự tải file về rồi gửi cho provider, người gọi không cần tự tải.
        Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken);
    }
}
