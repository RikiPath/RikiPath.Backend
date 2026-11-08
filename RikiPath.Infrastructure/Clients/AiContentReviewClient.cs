using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    /// <summary>
    /// Cấu hình AI kiểm tra nội dung. Bind từ appsettings: "AiContentReview".
    /// Dùng được với mọi nhà cung cấp theo chuẩn OpenAI "chat/completions":
    /// OpenAI, Groq, OpenRouter, DeepSeek, Together, Gemini (endpoint OpenAI-compatible), Ollama...
    /// </summary>
    public class AiContentReviewOptions
    {
        public const string SectionName = "AiContentReview";

        /// <summary>Ví dụ: https://api.openai.com/v1/ | https://api.groq.com/openai/v1/ | https://openrouter.ai/api/v1/</summary>
        public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o-mini";
        /// <summary>Thấp để kết quả kiểm tra ổn định, ít "sáng tạo".</summary>
        public double Temperature { get; set; } = 0.1;
        public int MaxOutputTokens { get; set; } = 4096;
        public int TimeoutSeconds { get; set; } = 90;
        public int MaxAttempts { get; set; } = 2;
        /// <summary>Bật response_format=json_object. Tắt nếu nhà cung cấp/model không hỗ trợ.</summary>
        public bool UseJsonMode { get; set; } = true;
    }

    public class AiContentReviewClient : IAiContentReviewClient
    {
        private const string SystemPrompt =
            "Bạn là biên tập viên kiểm duyệt nội dung tiếng Nhật (JLPT) cho người Việt. " +
            "Luôn trả lời CHỈ bằng một đối tượng JSON hợp lệ, không markdown, không giải thích bên ngoài JSON.";

        private readonly HttpClient _http;
        private readonly AiContentReviewOptions _options;
        private readonly ILogger<AiContentReviewClient> _logger;

        public AiContentReviewClient(
            HttpClient http,
            IOptions<AiContentReviewOptions> options,
            ILogger<AiContentReviewClient> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException("Thiếu cấu hình AiContentReview:ApiKey.");

            var baseUrl = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";
            _http.BaseAddress = new Uri(baseUrl);
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        public async Task<string> ReviewContentJsonAsync(int userId, string prompt, CancellationToken cancellationToken)
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = _options.Model,
                ["temperature"] = _options.Temperature,
                ["max_tokens"] = _options.MaxOutputTokens,
                ["messages"] = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = prompt },
                },
            };
            if (_options.UseJsonMode)
                body["response_format"] = new { type = "json_object" };

            var payload = JsonSerializer.Serialize(body);
            Exception? lastError = null;

            for (var attempt = 1; attempt <= Math.Max(1, _options.MaxAttempts); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                    };
                    using var response = await _http.SendAsync(request, cancellationToken);
                    var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Hết quota / bị giới hạn tốc độ: không retry, báo cho service trả 429 cho FE.
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        throw new AiQuotaExceededException("Dịch vụ AI đang quá tải hoặc đã hết hạn mức, vui lòng thử lại sau.");

                    if ((int)response.StatusCode >= 500)
                    {
                        lastError = new HttpRequestException($"AI trả về {(int)response.StatusCode}: {Truncate(raw)}");
                        _logger.LogWarning("AI content review 5xx (attempt {Attempt}). UserId={UserId}. {Body}",
                            attempt, userId, Truncate(raw));
                        await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"AI trả về {(int)response.StatusCode}: {Truncate(raw)}");

                    return ExtractContent(raw, userId);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout của HttpClient
                    lastError = ex;
                    _logger.LogWarning(ex, "AI content review timeout (attempt {Attempt}). UserId={UserId}", attempt, userId);
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "AI content review request failed (attempt {Attempt}). UserId={UserId}", attempt, userId);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                }
            }

            throw new InvalidOperationException("Dịch vụ AI kiểm tra nội dung không phản hồi.", lastError);
        }

        private string ExtractContent(string raw, int userId)
        {
            using var doc = JsonDocument.Parse(raw);
            var choice = doc.RootElement.GetProperty("choices")[0];

            if (choice.TryGetProperty("finish_reason", out var finish) &&
                finish.ValueKind == JsonValueKind.String && finish.GetString() == "length")
                _logger.LogWarning("AI content review bị cắt do chạm MaxOutputTokens (JSON có thể không hợp lệ). UserId={UserId}", userId);

            return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
    }
}