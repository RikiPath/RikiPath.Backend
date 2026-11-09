using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients;

/// <summary>
/// Client gọi Claude để chấm bài luận (作文) của học viên. Cùng cấu trúc đã chuẩn hóa với
/// ClaudeContentAuthoringClient/ClaudeLearningPathClient (timeout cấu hình được, tắt thinking,
/// ưu tiên đọc config Claude:* trước, fallback OpenRouter:*).
/// </summary>
public sealed class AIEssayGradingClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IOptions<OpenRouterSettings> providerOptions,
    IOptions<AiSettings> aiOptions) : IAiEssayGradingClient
{
    private const int MaxCompletionTokens = 6000;
    private const int DefaultTimeoutSeconds = 180;
    private const string DefaultClaudeModel = "anthropic/claude-3-5-haiku";

    public async Task<string> GenerateGradingJsonAsync(
        int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var settings = providerOptions.Value;
        var apiKey = configuration["CLAUDE_API_KEY"] ??
                     configuration["Claude:ApiKey"] ??
                     configuration["OPENROUTER_API_KEY"] ??
                     configuration["OpenRouter:ApiKey"] ??
                     settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiServiceException(
                "Thiếu API key cho Claude. Hãy cấu hình Claude:ApiKey hoặc CLAUDE_API_KEY " +
                "(tạm thời vẫn gọi qua OpenRouter nên OpenRouter:ApiKey/OPENROUTER_API_KEY cũng dùng được).");

        var baseUrl = configuration["Claude:BaseUrl"] ?? configuration["OpenRouter:BaseUrl"] ?? settings.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
            throw new AiServiceException("Claude:BaseUrl (hoặc OpenRouter:BaseUrl) không phải URL HTTP/HTTPS hợp lệ.");

        var model = configuration["Claude:GradingModel"];
        if (string.IsNullOrWhiteSpace(model)) model = configuration["Claude:Model"];
        if (string.IsNullOrWhiteSpace(model)) model = configuration["OpenRouter:Model"];
        if (string.IsNullOrWhiteSpace(model)) model = settings.Model;
        if (string.IsNullOrWhiteSpace(model)) model = DefaultClaudeModel;

        var root = baseUri.AbsoluteUri.TrimEnd('/') + "/";
        if (root.EndsWith("/chat/completions/", StringComparison.OrdinalIgnoreCase))
            root = root[..^"chat/completions/".Length];
        var endpoint = new Uri(new Uri(root), "chat/completions");

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            temperature = 0.2,
            max_tokens = MaxCompletionTokens,
            response_format = new { type = "json_object" },
            reasoning = new { exclude = true, effort = "low" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://rikipath.com");

        var appName = configuration["Claude:AppName"] ?? configuration["OpenRouter:AppName"] ?? settings.AppName;
        if (!string.IsNullOrWhiteSpace(appName))
            request.Headers.TryAddWithoutValidation("X-Title", appName);

        var timeoutSecondsText = configuration["Claude:TimeoutSeconds"] ?? configuration["OpenRouter:TimeoutSeconds"];
        var timeoutSeconds = int.TryParse(timeoutSecondsText, out var parsedTimeout) && parsedTimeout > 0
            ? parsedTimeout
            : DefaultTimeoutSeconds;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException(
                $"Claude (model '{model}') phản hồi quá thời gian chờ {timeoutSeconds} giây. Có thể đang bị xếp hàng đợi/chạy chậm; cân nhắc tăng Claude:TimeoutSeconds hoặc đổi model.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.PaymentRequired)
                    throw new AiQuotaExceededException(
                        "Claude đã giới hạn hoặc không đủ số dư cho yêu cầu. Kiểm tra quota/billing rồi thử lại.");

                throw new AiServiceException(
                    $"Claude lỗi {(int)response.StatusCode} với model '{model}': {body}");
            }

            try
            {
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.GetProperty("message").GetString();
                    throw new AiServiceException($"Claude trả về lỗi: {errorMessage}");
                }

                if (!document.RootElement.TryGetProperty("choices", out var choicesElement) ||
                    choicesElement.GetArrayLength() == 0)
                {
                    throw new AiServiceException($"Claude trả về phản hồi thiếu mảng 'choices'. Nội dung raw: {body}");
                }

                var firstChoice = choicesElement[0];
                var messageElement = firstChoice.GetProperty("message");
                var finishReason = firstChoice.TryGetProperty("finish_reason", out var finishReasonElement)
                    ? finishReasonElement.GetString()
                    : null;

                if (!messageElement.TryGetProperty("content", out var contentElement) ||
                    contentElement.ValueKind == JsonValueKind.Null)
                {
                    if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                        throw new AiServiceException(
                            $"Claude bị cắt phản hồi giữa chừng (finish_reason=length) trước khi tạo ra nội dung JSON — " +
                            $"model '{model}' có thể đã dùng hết token cho phần suy luận nội bộ (thinking). " +
                            $"Hãy tăng max_tokens hoặc tắt thinking. Nội dung raw: {body}");

                    throw new AiServiceException($"Claude trả về phản hồi thiếu thuộc tính 'content'. Nội dung raw: {body}");
                }

                if (contentElement.ValueKind == JsonValueKind.String)
                {
                    var text = contentElement.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        throw new AiServiceException($"Claude trả về nội dung rỗng. Nội dung raw: {body}");
                    return text;
                }

                if (contentElement.ValueKind == JsonValueKind.Array)
                {
                    var joined = string.Join("\n", contentElement.EnumerateArray()
                        .Where(item => item.TryGetProperty("text", out _))
                        .Select(item => item.GetProperty("text").GetString())
                        .Where(text => !string.IsNullOrWhiteSpace(text)));

                    if (string.IsNullOrWhiteSpace(joined))
                        throw new AiServiceException($"Claude trả về nội dung rỗng. Nội dung raw: {body}");
                    return joined;
                }

                throw new AiServiceException($"Claude trả về định dạng nội dung không hỗ trợ. Nội dung raw: {body}");
            }
            catch (JsonException ex)
            {
                throw new AiServiceException($"Không đọc được phản hồi JSON từ Claude. Raw body: {body}", ex);
            }
        }
    }
}