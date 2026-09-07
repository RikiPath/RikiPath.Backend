using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients
{
    // Gọi Groq Chat Completions API (tương thích OpenAI). Model + BaseUrl/ApiKey đọc từ
    // AppSettings.Ai - không còn đọc trực tiếp IConfiguration["OpenAI:Model"] như trước.
    //
    // LƯU Ý VỀ ĐỘ CHÍNH XÁC: xem comment chi tiết trong AiGradingClient - các cơ chế ở đây
    // (temperature = 0, response_format = json_object, validate + retry 1 lần) chỉ đảm bảo output
    // luôn là JSON đúng hình thức, KHÔNG đảm bảo nội dung gợi ý lộ trình học luôn đúng 100%.
    public class AiLearningPathClient(
        HttpClient httpClient,
        IOptions<AppSettings> appSettings,
        IAiUsageQuotaService quotaService) : IAiLearningPathClient
    {
        private readonly AiSettings _ai = appSettings.Value.Ai;

        private const string SystemPrompt =
            "Bạn là trợ lý cố vấn lộ trình học tiếng Nhật, chỉ trả JSON đúng schema được yêu cầu, " +
            "không kèm giải thích, không kèm markdown code fence.";

        public async Task<string> GenerateLearningPathJsonAsync(
            int userId, string prompt, CancellationToken cancellationToken)
        {
            await quotaService.EnsureWithinQuotaAsync(userId, AiFeature.LearningPath, cancellationToken);

            var content = await CallModelAsync(userId, prompt, cancellationToken);

            if (!IsValidJson(content))
            {
                content = await CallModelAsync(
                    userId,
                    prompt + "\n\n(Lần trước bạn trả lời không đúng JSON. Chỉ trả JSON object hợp lệ, " +
                             "không kèm text hay markdown nào khác.)",
                    cancellationToken);

                if (!IsValidJson(content))
                {
                    throw new AiServiceException(
                        "Dịch vụ AI trả về nội dung không phải JSON hợp lệ sau khi đã retry.");
                }
            }

            return content;
        }

        private async Task<string> CallModelAsync(int userId, string userPrompt, CancellationToken cancellationToken)
        {
            var payload = new
            {
                model = _ai.LearningPathModel,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userPrompt },
                },
                response_format = new { type = "json_object" },
                temperature = 0,
            };

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AiServiceException("Không thể kết nối tới dịch vụ AI.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AiServiceException($"Dịch vụ AI trả lỗi ({(int)response.StatusCode}): {errorBody}");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var root = doc.RootElement;

            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                throw new AiServiceException("Dịch vụ AI trả về nội dung rỗng.");

            // Ghi nhận quota SAU khi gọi thành công, dùng số token thật provider trả về.
            if (root.TryGetProperty("usage", out var usageElement) &&
                usageElement.TryGetProperty("total_tokens", out var totalTokensElement))
            {
                await quotaService.RecordUsageAsync(
                    userId, AiFeature.LearningPath, totalTokensElement.GetInt32(), cancellationToken);
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
    }
}