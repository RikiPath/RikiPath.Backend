using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Domain;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    public class AiLearningPathClient(
        HttpClient httpClient,
        IOptions<AppSettings> options,
        IConfiguration configuration) : IAiLearningPathClient
    {
        // ✅ Dọn dẹp chỉ giữ các model Gemini chuẩn đang hoạt động
        private static readonly string[] FallbackEndpoints = new[]
        {
            // Thay thế bằng tên model chính xác đang hiển thị trong tài khoản Google AI Studio của bạn
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent"
        };

        private readonly HttpClient _httpClient = httpClient;
        private readonly AppSettings _appSettings = options.Value;
        private readonly IConfiguration _configuration = configuration;
        private static int _keyIndexCounter = 0;

        private static readonly string[] SectionPaths = { "Gemini", "GeminiSettings", "AppSettings:Gemini", "AppSettings:GeminiSettings" };
        private static readonly string[] KeyNames = { "ApiKey", "ApiKeys" };

        // ✅ SystemPrompt mới có cấu trúc dailySchedules kèm từ vựng / kanji / ngữ pháp
        private const string SystemPrompt =
            "Bạn là trợ lý cố vấn lộ trình học tiếng Nhật chuyên nghiệp. " +
            "BẮT BUỘC chỉ trả về duy nhất 1 JSON object chuẩn theo đúng cấu trúc mẫu dưới đây, " +
            "không kèm markdown code fence (```json), không kèm giải thích ngoài JSON:\n" +
            "{\n" +
            "  \"summary\": \"Tóm tắt tổng quan về định hướng lộ trình học...\",\n" +
            "  \"focusTopics\": [\n" +
            "    \"Chủ đề trọng tâm 1\",\n" +
            "    \"Chủ đề trọng tâm 2\"\n" +
            "  ],\n" +
            "  \"dailySchedules\": [\n" +
            "    {\n" +
            "      \"day\": 1,\n" +
            "      \"title\": \"Tên bài học/chủ đề Ngày 1\",\n" +
            "      \"focus\": \"Mục tiêu chính cần đạt trong ngày\",\n" +
            "      \"lessons\": [\"Tên bài học hoặc hoạt động chính\"],\n" +
            "      \"vocabularies\": [\"Từ vựng 1 (Nghĩa)\", \"Từ vựng 2 (Nghĩa)\"],\n" +
            "      \"kanjis\": [\"Chữ Hán 1 (Âm Hán/Nghĩa)\"],\n" +
            "      \"grammars\": [\"Cấu trúc ngữ pháp 1\"]\n" +
            "    }\n" +
            "  ]\n" +
            "}";

        public async Task<string> GenerateLearningPathJsonAsync(
            int userId, string prompt, CancellationToken cancellationToken)
        {
            var content = await CallModelAsync(prompt, cancellationToken);

            if (!IsValidJson(content))
            {
                content = await CallModelAsync(
                    prompt + "\n\n(Lần trước bạn trả lời không đúng JSON. Chỉ trả JSON object hợp lệ, không kèm text hay markdown nào khác.)",
                    cancellationToken);

                if (!IsValidJson(content))
                {
                    throw new AiServiceException("Dịch vụ AI trả về nội dung không phải JSON hợp lệ sau khi đã retry.");
                }
            }

            return content;
        }

        private async Task<string> CallModelAsync(string userPrompt, CancellationToken cancellationToken)
        {
            var apiKeys = ResolveApiKeys();

            if (apiKeys.Count == 0)
            {
                throw new AiServiceException(
                    "Không đọc được Gemini ApiKey nào từ cấu hình. Trạng thái config: " + DescribeConfigState());
            }

            var endpointsToTry = BuildEndpointsToTry();

            int totalKeys = apiKeys.Count;
            int startIndex = (int)((uint)Interlocked.Increment(ref _keyIndexCounter) % (uint)totalKeys);

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = SystemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.0
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            string? lastError = null;

            foreach (var currentEndpoint in endpointsToTry)
            {
                for (int i = 0; i < totalKeys; i++)
                {
                    int currentIndex = (startIndex + i) % totalKeys;

                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    attemptCts.CancelAfter(TimeSpan.FromSeconds(30));

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

                            throw new AiServiceException($"Gemini không trả về nội dung văn bản. Phản hồi: {body}");
                        }

                        var statusCode = response.StatusCode;
                        lastError = $"Endpoint [{currentEndpoint}] Key [{currentIndex}] ({(int)statusCode}): {body}";

                        bool isModelUnavailable = statusCode == HttpStatusCode.ServiceUnavailable
                                               || statusCode == HttpStatusCode.NotFound
                                               || statusCode == HttpStatusCode.GatewayTimeout
                                               || body.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                                               || body.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
                                               || body.Contains("high demand", StringComparison.OrdinalIgnoreCase);

                        if (isModelUnavailable)
                        {
                            Console.WriteLine($"[WARN] Model [{currentEndpoint}] bị lỗi không khả dụng ({(int)statusCode}). Chuyển sang Model dự phòng tiếp theo...");
                            break;
                        }

                        bool shouldRotateKey = statusCode == HttpStatusCode.TooManyRequests
                                            || statusCode == HttpStatusCode.Forbidden
                                            || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                                            || body.Contains("quota", StringComparison.OrdinalIgnoreCase);

                        if (shouldRotateKey)
                        {
                            Console.WriteLine($"[WARN] Gemini Key [{currentIndex}] gặp lỗi ({(int)statusCode}). Đang xoay sang Key tiếp theo...");
                            continue;
                        }

                        throw new AiServiceException($"Lỗi Gemini API ({(int)statusCode}): {body}");
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[WARN] Endpoint [{currentEndpoint}] Key [{currentIndex}] bị Timeout 30s. Thử tiếp...");
                        lastError = $"Endpoint [{currentEndpoint}] Key [{currentIndex}] bị Timeout";
                        continue;
                    }
                }
            }

            throw new AiServiceException($"Tất cả Model ({endpointsToTry.Count}) và API Key ({totalKeys}) đều thất bại. Lỗi cuối: {lastError}");
        }

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

        private static bool IsValidJson(string content)
        {
            try
            {
                using var _ = JsonDocument.Parse(content);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}