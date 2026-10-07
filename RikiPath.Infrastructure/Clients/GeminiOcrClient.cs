using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RikiPath.Application.IClients;
using RikiPath.Domain;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    public class GeminiOcrClient(HttpClient httpClient, IOptions<AppSettings> options, IConfiguration configuration) : IGeminiOcrClient
    {
        private static readonly string[] FallbackEndpoints = new[]
         {
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent",
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent",
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent"
        };

        private readonly HttpClient _httpClient = httpClient;
        private readonly AppSettings _appSettings = options.Value;
        private readonly IConfiguration _configuration = configuration;
        private static int _keyIndexCounter = 0;
        private static readonly string[] SectionPaths = { "Gemini", "GeminiSettings", "AppSettings:Gemini", "AppSettings:GeminiSettings" };
        private static readonly string[] KeyNames = { "ApiKey", "ApiKeys" };

        private void AddKeysFrom(IConfigurationSection node, List<string> keys)
        {
            if (!string.IsNullOrWhiteSpace(node.Value))
            {
                keys.AddRange(node.Value.Split(new[] { ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            foreach (var child in node.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value)) keys.Add(child.Value.Trim());
            }
        }

        private List<string> ResolveApiKeys()
        {
            var keys = new List<string>();

            foreach (var path in SectionPaths)
                foreach (var name in KeyNames)
                    AddKeysFrom(_configuration.GetSection($"{path}:{name}"), keys);

            if (keys.Count == 0)
                AddKeysFrom(_configuration.GetSection("GEMINI_API_KEY"), keys);

            if (keys.Count == 0)
                keys.AddRange(_appSettings?.Gemini?.GetValidKeys() ?? new List<string>());

            return keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        }

        private List<string> BuildEndpointsToTry()
        {
            var endpoints = new List<string>();

            // Lấy BaseUrl từ config nếu có
            var configuredUrl = _appSettings?.Gemini?.BaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                configuredUrl = SectionPaths
                    .Select(p => _configuration[$"{p}:BaseUrl"])
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            }

            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                endpoints.Add(configuredUrl);
            }

            // Thêm các model dự phòng chưa có trong danh sách
            foreach (var fallback in FallbackEndpoints)
            {
                if (!endpoints.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                {
                    endpoints.Add(fallback);
                }
            }

            return endpoints;
        }

        private string DescribeConfigState()
        {
            string Children(string path) =>
                string.Join(", ", _configuration.GetSection(path).GetChildren().Select(c => c.Key));

            return $"Env={_configuration["ASPNETCORE_ENVIRONMENT"] ?? "(null)"}; " +
                   $"Gemini children=[{Children("Gemini")}]; " +
                   $"AppSettings:Gemini children=[{Children("AppSettings:Gemini")}]; " +
                   $"Root sections=[{string.Join(", ", _configuration.GetChildren().Select(c => c.Key))}]";
        }

        public async Task<string> ScanHandwritingAsync(string prompt, string base64Image, string mimeType, CancellationToken cancellationToken = default)
        {
            var apiKeys = ResolveApiKeys();

            if (apiKeys.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không đọc được Gemini ApiKey nào từ cấu hình. Trạng thái config: " + DescribeConfigState());
            }

            var endpointsToTry = BuildEndpointsToTry();

            int totalKeys = apiKeys.Count;
            int startIndex = (int)((uint)Interlocked.Increment(ref _keyIndexCounter) % (uint)totalKeys);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new { inline_data = new { mime_type = mimeType, data = base64Image } }
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            string? lastError = null;

            // Vòng lặp 1: Lần lượt thử qua từng Model Endpoint
            foreach (var currentEndpoint in endpointsToTry)
            {
                // Vòng lặp 2: Lần lượt thử các API Key cho Model hiện tại
                for (int i = 0; i < totalKeys; i++)
                {
                    int currentIndex = (startIndex + i) % totalKeys;

                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    attemptCts.CancelAfter(TimeSpan.FromSeconds(30)); // Giảm xuống 30s để nếu quá tải sẽ chuyển model nhanh hơn

                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, currentEndpoint)
                        {
                            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                        };
                        request.Headers.Add("x-goog-api-key", apiKeys[currentIndex]);

                        using var response = await _httpClient.SendAsync(request, attemptCts.Token);
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (response.IsSuccessStatusCode)
                        {
                            using var doc = JsonDocument.Parse(body);

                            if (doc.RootElement.TryGetProperty("candidates", out var candidates)
                                && candidates.GetArrayLength() > 0
                                && candidates[0].TryGetProperty("content", out var content)
                                && content.TryGetProperty("parts", out var parts)
                                && parts.GetArrayLength() > 0
                                && parts[0].TryGetProperty("text", out var textEl))
                            {
                                return textEl.GetString() ?? string.Empty;
                            }

                            throw new InvalidOperationException($"Gemini không trả về nội dung văn bản. Phản hồi: {body}");
                        }

                        var statusCode = response.StatusCode;
                        lastError = $"Endpoint [{currentEndpoint}] Key [{currentIndex}] ({(int)statusCode}): {body}";

                        // Kiểm tra xem có phải lỗi server Model bị quá tải 503 hay không
                        bool isModelOverloaded = statusCode == HttpStatusCode.ServiceUnavailable
                                                 || statusCode == HttpStatusCode.GatewayTimeout
                                                 || body.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
                                                 || body.Contains("high demand", StringComparison.OrdinalIgnoreCase);

                        if (isModelOverloaded)
                        {
                            Console.WriteLine($"[WARN] Model [{currentEndpoint}] bị quá tải (503 / High demand). Chuyển sang Model dự phòng tiếp theo...");
                            // Nếu Model đã bị quá tải, thử Key khác cùng Model cũng sẽ lỗi 503.
                            // Do đó 'break' ngay vòng lặp Key để nhảy sang Model tiếp theo!
                            break;
                        }

                        // Nếu là lỗi Quota/Key (429, 403...) thì mới tiếp tục xoay sang Key tiếp theo
                        bool shouldRotateKey = statusCode == HttpStatusCode.TooManyRequests
                                            || statusCode == HttpStatusCode.Forbidden
                                            || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                                            || body.Contains("quota", StringComparison.OrdinalIgnoreCase);

                        if (shouldRotateKey)
                        {
                            Console.WriteLine($"[WARN] Gemini Key [{currentIndex}] gặp lỗi ({(int)statusCode}). Đang xoay sang Key tiếp theo...");
                            continue;
                        }

                        throw new HttpRequestException($"Lỗi Gemini API ({(int)statusCode}): {body}");
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[WARN] Endpoint [{currentEndpoint}] Key [{currentIndex}] bị Timeout 30s. Thử tiếp...");
                        lastError = $"Endpoint [{currentEndpoint}] Key [{currentIndex}] bị Timeout";
                        continue;
                    }
                }
            }

            throw new InvalidOperationException($"Tất cả Model ({endpointsToTry.Count}) và API Key ({totalKeys}) đều thất bại. Lỗi cuối: {lastError}");
        }
    }
}