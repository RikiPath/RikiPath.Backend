// Đặt tại: RikiPath.Infrastructure/Clients/AzureSpeechToTextClient.cs  (thay file cũ)
// Chỉ được có MỘT class implement IAzureSpeechClient - nếu còn file AzureSpeechClient.cs (bản trước) thì XOÁ đi,
// vì Program.cs ưu tiên class tên "AzureSpeechClient" và sẽ âm thầm bỏ qua class này.
using RikiPath.Application.IClients;
using RikiPath.Application.Responses.SpeechToText;
using RikiPath.Domain;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    public class AzureSpeechToTextClient(IHttpClientFactory httpClientFactory, AppSettings appSettings) : IAzureSpeechClient
    {
        private const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";
        private const string DefaultLanguage = "ja-JP";

        private const string AudioContentType = "audio/wav; codecs=audio/pcm; samplerate=16000";

        private const long MaxAudioBytes = 10 * 1024 * 1024;

        private (string Region, string Key, string Language) GetSettings()
        {
            var settings = appSettings.AzureSpeechSettings
                ?? throw new InvalidOperationException("AzureSpeech chưa được cấu hình trong appsettings.");

            if (string.IsNullOrWhiteSpace(settings.Region))
                throw new InvalidOperationException("AzureSpeech:Region chưa được cấu hình.");

            if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
                throw new InvalidOperationException("AzureSpeech:SubscriptionKey chưa được cấu hình.");

            var language = string.IsNullOrWhiteSpace(settings.Language) ? DefaultLanguage : settings.Language.Trim();
            return (settings.Region.Trim(), settings.SubscriptionKey.Trim(), language);
        }

        // ---------------------------------------------------------------------------------------
        // 1) Lấy token tạm thời cho FE
        // ---------------------------------------------------------------------------------------
        public async Task<string> IssueTokenAsync(CancellationToken cancellationToken = default)
        {
            var (region, key, _) = GetSettings();

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"https://{region}.api.cognitive.microsoft.com/sts/v1.0/issueToken")
            {
                // Body rỗng nhưng vẫn gửi Content-Length: 0 (Azure có thể trả 411 nếu thiếu)
                Content = new StringContent(string.Empty)
            };
            request.Headers.Add(SubscriptionKeyHeader, key);

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Azure Speech issueToken trả về {(int)response.StatusCode}.", null, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        // ---------------------------------------------------------------------------------------
        // 2) Speech-to-Text từ URL audio
        // ---------------------------------------------------------------------------------------
        public async Task<SpeechToTextResult> TranscribeFromUrlAsync(string audioUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(audioUrl))
                throw new ArgumentException("audioUrl không được để trống.", nameof(audioUrl));

            if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var audioUri)
                || (audioUri.Scheme != Uri.UriSchemeHttps && audioUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException("audioUrl phải là URL http/https tuyệt đối.", nameof(audioUrl));
            }

            var (region, key, language) = GetSettings();

            var audioBytes = await DownloadAudioAsync(audioUri, cancellationToken);

            var requestUrl =
                $"https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1" +
                $"?language={Uri.EscapeDataString(language)}&format=detailed";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new ByteArrayContent(audioBytes)
            };
            request.Content.Headers.TryAddWithoutValidation("Content-Type", AudioContentType);
            request.Headers.Add(SubscriptionKeyHeader, key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var azureClient = httpClientFactory.CreateClient();
            using var response = await azureClient.SendAsync(request, cancellationToken);
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

        /// <summary>
        /// Tải audio bằng một HttpClient RIÊNG, không mang Subscription Key,
        /// để key Azure không bị gửi sang server lưu trữ (Supabase...).
        /// </summary>
        private async Task<byte[]> DownloadAudioAsync(Uri audioUri, CancellationToken cancellationToken)
        {
            var downloadClient = httpClientFactory.CreateClient();
            using var response = await downloadClient.GetAsync(
                audioUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Không tải được file audio ({(int)response.StatusCode}).");

            if (response.Content.Headers.ContentLength is > MaxAudioBytes)
                throw new InvalidOperationException("File audio quá lớn (tối đa 10MB, khoảng 60 giây).");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes.Length > MaxAudioBytes)
                throw new InvalidOperationException("File audio quá lớn (tối đa 10MB, khoảng 60 giây).");

            return bytes;
        }
    }
}