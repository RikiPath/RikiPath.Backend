using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Domain;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RikiPath.Infrastructure.Clients
{
    // Gọi OpenAI Chat Completions API (dùng chung HttpClient BaseAddress/Authorization đã cấu hình
    // sẵn ở Program.cs qua OpenAI:BaseUrl / OpenAI:ApiKey - giống cách AiLearningPathClient đang dùng).
    // Không throw-swallow lỗi ở đây: để lỗi HTTP/provider bubble lên PracticeSubmissionService,
    // nơi đã có try-catch bọc ngoài (không làm hỏng bài nộp, chỉ GradingResult vẫn null).
    public class AiGradingClient(
       HttpClient httpClient,
       IOptions<AppSettings> appSettings,
       IAiUsageQuotaService quotaService) : IAiGradingClient
    {
        private readonly AiSettings _ai = appSettings.Value.Ai;

        private const string SystemPrompt =
            "Bạn là giám khảo chấm bài luyện tập tiếng Nhật theo rubric JLPT. " +
            "Luôn trả lời CHỈ một JSON object hợp lệ đúng schema được yêu cầu, không kèm markdown " +
            "code fence, không kèm bất kỳ text giải thích nào khác ngoài JSON.";

        public async Task<string> GradeSubmissionJsonAsync(
            int userId, string promptText, CancellationToken cancellationToken)
        {
            await quotaService.EnsureWithinQuotaAsync(userId, AiFeature.Grading, cancellationToken);

            var content = await CallModelAsync(userId, promptText, cancellationToken);

            if (!IsValidJson(content))
            {
                // Retry đúng 1 lần với lời nhắc nhấn mạnh yêu cầu JSON thuần, trước khi bó tay.
                content = await CallModelAsync(
                    userId,
                    promptText + "\n\n(Lần trước bạn trả lời không đúng JSON. Chỉ trả JSON object hợp lệ, " +
                                  "không kèm text hay markdown nào khác.)",
                    cancellationToken);

                if (!IsValidJson(content))
                {
                    throw new AiServiceException(
                        "AI grading provider trả về nội dung không phải JSON hợp lệ sau khi đã retry.");
                }
            }

            return content;
        }

        private async Task<string> CallModelAsync(int userId, string userPrompt, CancellationToken cancellationToken)
        {
            var payload = new ChatCompletionRequest
            {
                Model = _ai.GradingModel,
                Temperature = 0,
                ResponseFormat = new ResponseFormat(),
                Messages =
                [
                    new ChatMessage { Role = "system", Content = SystemPrompt },
                    new ChatMessage { Role = "user", Content = userPrompt }
                ]
            };

            using var response = await httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);

            // Không catch ở đây: HttpRequestException/non-2xx sẽ bubble lên caller, đúng theo
            // hợp đồng doc-comment của IAiGradingClient ("phải NOT swallow provider errors").
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                cancellationToken: cancellationToken);

            var content = result?.Choices?.Count > 0 ? result.Choices[0].Message?.Content : null;

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("AI grading provider trả về nội dung rỗng.");

            // Ghi nhận quota SAU khi gọi thành công, dùng số token thật provider trả về (không ước
            // lượng) - nếu provider không trả usage vì lý do nào đó, chỉ tăng request count qua lần
            // EnsureWithinQuotaAsync kế tiếp, không đoán số token.
            if (result?.Usage is not null)
            {
                await quotaService.RecordUsageAsync(
                    userId, AiFeature.Grading, result.Usage.TotalTokens, cancellationToken);
            }

            return content;
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

        // DTO nội bộ cho Groq/OpenAI chat completions - không expose ra ngoài Infrastructure.
        private class ChatCompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<ChatMessage> Messages { get; set; } = [];

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("response_format")]
            public ResponseFormat? ResponseFormat { get; set; }
        }

        private class ResponseFormat
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "json_object";
        }

        private class ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<ChatChoice> Choices { get; set; } = [];

            [JsonPropertyName("usage")]
            public UsageInfo? Usage { get; set; }
        }

        private class ChatChoice
        {
            [JsonPropertyName("message")]
            public ChatMessage? Message { get; set; }
        }

        private class UsageInfo
        {
            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }
    }
}