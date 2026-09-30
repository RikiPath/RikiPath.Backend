using RikiPath.Application.IClients;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Domain;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    // ⚠️ GIẢ ĐỊNH QUAN TRỌNG - ĐỌC TRƯỚC KHI DÙNG:
    //
    // 1) Dùng REST API "short audio" (endpoint /speech/recognition/conversation/.../v1) - đây là
    //    API ĐỒNG BỘ, Azure giới hạn khoảng dưới 60 giây/file. Phù hợp cho bài luyện nói ngắn
    //    (từng câu/đoạn ngắn JLPT speaking). Nếu sau này cần ghi âm DÀI HƠN (vd bài nói 2-3 phút),
    //    phải chuyển sang Batch Transcription API (bất đồng bộ, cần Azure Blob Storage riêng,
    //    polling job status...) - phức tạp hơn nhiều, báo tôi nếu cần bản đó.
    //
    // 2) Content-Type mặc định set cứng "audio/wav; codecs=audio/pcm; samplerate=16000" - đây là
    //    format AN TOÀN NHẤT được Azure hỗ trợ chắc chắn (WAV PCM 16kHz mono 16-bit). Azure cũng hỗ
    //    trợ "audio/ogg; codecs=opus" và "audio/webm; codecs=opus", nhưng KHÔNG hỗ trợ mp3/m4a trực
    //    tiếp. Nếu app di động/web của bạn ghi âm ra định dạng khác WAV, BẮT BUỘC phải:
    //      (a) convert sang WAV PCM 16kHz mono trước khi upload lên Supabase, HOẶC
    //      (b) đổi hằng số AudioContentType bên dưới cho đúng định dạng thật bạn dùng.
    //    Nếu không, Azure sẽ trả lỗi hoặc RecognitionStatus khác "Success".
    //
    // 3) Free Tier (F0) giới hạn 5 giờ audio/tháng + rate limit ~20 request/phút - cân nhắc thêm
    //    retry/backoff ở tầng gọi nếu gặp lỗi 429 khi lên production.
    public class AzureSpeechToTextClient : ISpeechToTextClient
    {
        private const string AudioContentType = "audio/wav; codecs=audio/pcm; samplerate=16000";

        private readonly HttpClient _httpClient;
        private readonly string _language;

        public AzureSpeechToTextClient(HttpClient httpClient, AppSettings appSettings)
        {
            _httpClient = httpClient;

            var settings = appSettings.AzureSpeech
                ?? throw new InvalidOperationException("AzureSpeech chưa được cấu hình trong appsettings.");

            if (string.IsNullOrWhiteSpace(settings.Region))
                throw new InvalidOperationException("AzureSpeech:Region chưa được cấu hình.");

            if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
                throw new InvalidOperationException("AzureSpeech:SubscriptionKey chưa được cấu hình.");

            _language = string.IsNullOrWhiteSpace(settings.Language) ? "ja-JP" : settings.Language;

            _httpClient.BaseAddress ??= new Uri($"https://{settings.Region}.stt.speech.microsoft.com");
            _httpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", settings.SubscriptionKey);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(audioUrl))
                throw new ArgumentException("audioUrl không được để trống.", nameof(audioUrl));

            // Tải file audio về. Lưu ý: _httpClient đã set BaseAddress trỏ tới Azure, nhưng vì
            // audioUrl là URL tuyệt đối (vd https://xxx.supabase.co/...) nên HttpClient sẽ gọi
            // thẳng tới đó, KHÔNG bị ghép với BaseAddress.
            var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl, cancellationToken);

            using var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(AudioContentType);

            var requestUrl = $"/speech/recognition/conversation/cognitiveservices/v1?language={_language}&format=detailed";

            using var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Azure Speech-to-Text lỗi ({(int)response.StatusCode}): {rawJson}");

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var recognitionStatus = root.TryGetProperty("RecognitionStatus", out var statusProp)
                ? statusProp.GetString()
                : null;

            if (!string.Equals(recognitionStatus, "Success", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Azure không nhận diện được giọng nói (RecognitionStatus = {recognitionStatus ?? "null"}). " +
                    $"Kiểm tra lại định dạng audio (xem comment AudioContentType). Raw: {rawJson}");

            var displayText = root.TryGetProperty("DisplayText", out var textProp) ? textProp.GetString() ?? "" : "";

            var confidence = 0d;
            if (root.TryGetProperty("NBest", out var nBestProp) &&
                nBestProp.ValueKind == JsonValueKind.Array &&
                nBestProp.GetArrayLength() > 0 &&
                nBestProp[0].TryGetProperty("Confidence", out var confProp))
            {
                confidence = confProp.GetDouble();
            }

            return new SpeechToTextResult(displayText, confidence, rawJson);
        }
    }
}