using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using OpenAiSettings = RikiPath.Domain.OpenAiSettings;

namespace RikiPath.Infrastructure.Clients;

/// <summary>Shared OpenAI transport. Never falls back to another provider or another API key.</summary>
public sealed class OpenAiClient(HttpClient http, IOptions<OpenAiSettings> options, ILogger<OpenAiClient>? logger = null)
{
    public OpenAiSettings Settings => options.Value;

    public HttpRequestMessage CreateRequest(string path, HttpContent content)
    {
        if (string.IsNullOrWhiteSpace(Settings.ApiKey))
            throw new AiServiceException("Thiếu OpenAI API key. Cấu hình OpenAI:ApiKey hoặc OPENAI_API_KEY trên máy chủ.");
        if (!Uri.TryCreate(Settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
            root.Scheme != Uri.UriSchemeHttps)
            throw new AiServiceException("OpenAI:BaseUrl phải là URL HTTPS hợp lệ.");
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
        return request;
    }

    public async Task<(string Text, int TotalTokens)> CompleteAsync(
        string model, string systemPrompt, object userContent, bool json, CancellationToken cancellationToken,
        string? reasoningEffort = null, int? maxOutputTokens = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            ["max_completion_tokens"] = maxOutputTokens ?? Settings.MaxOutputTokens,
            ["store"] = false
        };
        if (json) payload["response_format"] = new { type = "json_object" };
        if (!string.IsNullOrWhiteSpace(reasoningEffort)) payload["reasoning_effort"] = reasoningEffort;
        using var request = CreateRequest("chat/completions", JsonContent.Create(payload));
        logger?.LogInformation("Sending OpenAI completion. Model={Model}", model);
        var raw = await SendAsync(request, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(raw);
            var choice = document.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() == "length")
                throw new AiServiceException("OpenAI đã chạm giới hạn token. Tăng OpenAI:MaxOutputTokens hoặc giảm nội dung yêu cầu.");
            var message = choice.GetProperty("message");
            if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String)
                throw new AiServiceException("OpenAI từ chối xử lý yêu cầu này.");
            var text = message.GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(text)) throw new AiServiceException("OpenAI trả về nội dung rỗng.");
            if (json)
            {
                using var result = JsonDocument.Parse(text);
                if (result.RootElement.ValueKind != JsonValueKind.Object)
                    throw new AiServiceException("OpenAI không trả về JSON object hợp lệ.");
            }
            var tokens = document.RootElement.TryGetProperty("usage", out var usage) &&
                         usage.TryGetProperty("total_tokens", out var total) ? total.GetInt32() : 0;
            return (text, tokens);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiServiceException("Không đọc được phản hồi OpenAI hợp lệ.", ex);
        }
    }

    public async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, Settings.TimeoutSeconds)));
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            var requestId = response.Headers.TryGetValues("x-request-id", out var ids) ? ids.FirstOrDefault() : null;
            logger?.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "OpenAI HTTP response. Host={Host} Status={Status} OpenAiRequestId={OpenAiRequestId}",
                request.RequestUri?.Host, (int)response.StatusCode, requestId);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.PaymentRequired)
                throw new AiQuotaExceededException("OpenAI đã giới hạn yêu cầu hoặc hết hạn mức API. Kiểm tra billing và quota OpenAI API.");
            if (!response.IsSuccessStatusCode)
                throw new AiServiceException($"OpenAI API trả về lỗi {(int)response.StatusCode}. Kiểm tra API key, quyền truy cập model và cấu hình tài khoản.");
            return body;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException("OpenAI phản hồi quá thời gian chờ. Có thể điều chỉnh OpenAI:TimeoutSeconds.");
        }
    }
}
