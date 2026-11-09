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

/// <summary>OpenRouter chat-completions client for personalized learning-path generation.</summary>
public sealed class OpenRouterAiLearningPathClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IOptions<OpenRouterSettings> openRouterOptions,
    IOptions<AiSettings> aiOptions) : IAiLearningPathClient
{
    private const string SystemPrompt = """
        Bạn là cố vấn ôn luyện JLPT. Chỉ phân tích dữ liệu Placement, Mock Test, mục tiêu, tiến độ và danh mục lesson mà máy chủ cung cấp; không tra cứu hoặc thêm dữ liệu bên ngoài. Hãy đánh giá thẳng thắn năng lực hiện tại theo từng kỹ năng, chỉ ra phần cần cải thiện để đạt chứng chỉ mục tiêu, và nhận xét khả năng kịp ngày thi nếu có. Chỉ chọn lesson ID có trong danh mục database được gửi kèm. Sắp thứ tự từ nền tảng hiện tại đến cấp độ mục tiêu. Không khẳng định chắc chắn đỗ; coi kết quả Placement là ước lượng năng lực.
        Chỉ trả về một JSON object hợp lệ, không Markdown, với đúng các khóa: summary (string), strengths (string[]), weaknesses (string[]), feasibility (string), isExamDateRealistic (boolean hoặc null), recommendedStudyDays (integer), focusTopics (string[]), dailySchedules (array gồm day:int, title:string, focus:string, lessonIds:int[]).
        """;

    // Vùng đệm token cho toàn bộ phản hồi (bao gồm cả phần "reasoning" nếu model không tắt được nó
    // qua tham số reasoning.exclude). Một số model free trên OpenRouter (vd: các bản *-vl reasoning)
    // tiêu tốn hàng nghìn token để "suy nghĩ" trước khi viết JSON thật, nên để thấp dễ bị cắt giữa chừng
    // (finish_reason = "length", content = null).
    private const int MaxCompletionTokens = 8000;

    public async Task<string> GenerateLearningPathJsonAsync(
        int userId, string prompt, CancellationToken cancellationToken)
    {
        var settings = openRouterOptions.Value;
        var apiKey = configuration["OPENROUTER_API_KEY"] ??
                     configuration["OpenRouter:ApiKey"] ??
                     settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiServiceException(
                "Thiếu OpenRouter API key. Hãy cấu hình OpenRouter:ApiKey hoặc OPENROUTER_API_KEY.");

        var baseUrl = configuration["OpenRouter:BaseUrl"] ?? settings.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
            throw new AiServiceException("OpenRouter:BaseUrl không phải URL HTTP/HTTPS hợp lệ.");

        var model = configuration["OpenRouter:Model"];
        if (string.IsNullOrWhiteSpace(model)) model = settings.Model;
        if (string.IsNullOrWhiteSpace(model)) model = configuration["Ai:LearningPathModel"];
        if (string.IsNullOrWhiteSpace(model)) model = aiOptions.Value.LearningPathModel;
        if (string.IsNullOrWhiteSpace(model))
            throw new AiServiceException("Thiếu tên model OpenRouter. Hãy cấu hình OpenRouter:Model hoặc Ai:LearningPathModel.");

        var root = baseUri.AbsoluteUri.TrimEnd('/') + "/";
        if (root.EndsWith("/chat/completions/", StringComparison.OrdinalIgnoreCase))
            root = root[..^"chat/completions/".Length];
        var endpoint = new Uri(new Uri(root), "chat/completions");

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = prompt },
            },
            temperature = 0.1,
            max_tokens = MaxCompletionTokens,
            response_format = new { type = "json_object" },
            // Tắt (hoặc giảm tối đa) bước "suy nghĩ" nội bộ của các model reasoning (vd: *-vl, o-series,
            // deepseek-r1...). Nếu model không hỗ trợ tham số này, OpenRouter sẽ bỏ qua an toàn chứ không lỗi.
            // Đây là nguyên nhân chính khiến content trả về null: model dùng hết token cho "reasoning"
            // trước khi kịp viết JSON thật vào "content".
            reasoning = new { exclude = true, effort = "low" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Thêm HTTP-Referer và X-Title để tuân thủ đúng yêu cầu định danh của OpenRouter
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://rikipath.com");

        var appName = configuration["OpenRouter:AppName"] ?? settings.AppName;
        if (!string.IsNullOrWhiteSpace(appName))
            request.Headers.TryAddWithoutValidation("X-Title", appName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException("OpenRouter phản hồi quá thời gian chờ 120 giây.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.PaymentRequired)
                    throw new AiQuotaExceededException(
                        "OpenRouter đã giới hạn hoặc không đủ số dư cho yêu cầu. Kiểm tra quota/billing rồi thử lại.");

                throw new AiServiceException(
                    $"OpenRouter lỗi {(int)response.StatusCode} với model '{model}': {body}");
            }

            try
            {
                using var document = JsonDocument.Parse(body);

                // Kiểm tra xem JSON trả về có chứa thông báo lỗi nội bộ hay không
                if (document.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.GetProperty("message").GetString();
                    throw new AiServiceException($"OpenRouter trả về lỗi từ Model: {errorMessage}");
                }

                if (!document.RootElement.TryGetProperty("choices", out var choicesElement) ||
                    choicesElement.GetArrayLength() == 0)
                {
                    throw new AiServiceException($"OpenRouter trả về phản hồi thiếu mảng 'choices'. Nội dung raw: {body}");
                }

                var firstChoice = choicesElement[0];
                var messageElement = firstChoice.GetProperty("message");
                var finishReason = firstChoice.TryGetProperty("finish_reason", out var finishReasonElement)
                    ? finishReasonElement.GetString()
                    : null;

                if (!messageElement.TryGetProperty("content", out var contentElement) ||
                    contentElement.ValueKind == JsonValueKind.Null)
                {
                    // Trường hợp phổ biến nhất: model reasoning dùng hết số token được cấp cho việc
                    // "suy nghĩ" (xem message.reasoning) nên bị cắt (finish_reason = "length") trước khi
                    // kịp viết JSON thật vào content. Báo rõ nguyên nhân để dễ chẩn đoán thay vì lỗi chung chung.
                    if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                        throw new AiServiceException(
                            $"OpenRouter bị cắt phản hồi giữa chừng (finish_reason=length) trước khi tạo ra nội dung JSON — " +
                            $"model '{model}' có thể đã dùng hết token cho phần suy luận nội bộ. " +
                            $"Hãy tăng max_tokens, dùng model khác, hoặc kiểm tra tham số 'reasoning'. Nội dung raw: {body}");

                    throw new AiServiceException($"OpenRouter trả về phản hồi thiếu thuộc tính 'content'. Nội dung raw: {body}");
                }

                if (contentElement.ValueKind == JsonValueKind.String)
                {
                    var text = contentElement.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        throw new AiServiceException($"OpenRouter trả về nội dung rỗng. Nội dung raw: {body}");
                    return text;
                }

                if (contentElement.ValueKind == JsonValueKind.Array)
                {
                    var joined = string.Join("\n", contentElement.EnumerateArray()
                        .Where(item => item.TryGetProperty("text", out _))
                        .Select(item => item.GetProperty("text").GetString())
                        .Where(text => !string.IsNullOrWhiteSpace(text)));

                    if (string.IsNullOrWhiteSpace(joined))
                        throw new AiServiceException($"OpenRouter trả về nội dung rỗng. Nội dung raw: {body}");
                    return joined;
                }

                throw new AiServiceException($"OpenRouter trả về định dạng nội dung không hỗ trợ. Nội dung raw: {body}");
            }
            catch (JsonException ex)
            {
                throw new AiServiceException($"Không đọc được phản hồi JSON từ OpenRouter. Raw body: {body}", ex);
            }
            catch (KeyNotFoundException ex)
            {
                throw new AiServiceException($"Phản hồi OpenRouter thiếu phần cấu trúc lộ trình. Raw body: {body}", ex);
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new AiServiceException($"OpenRouter trả về danh sách lựa chọn rỗng. Raw body: {body}", ex);
            }
        }
    }
}