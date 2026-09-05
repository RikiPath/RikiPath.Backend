using Microsoft.Extensions.Configuration;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using System.Net.Http.Json;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    public class AiLearningPathClient(HttpClient httpClient, IConfiguration configuration) : IAiLearningPathClient
    {
        public async Task<string> GenerateLearningPathJsonAsync(string prompt, CancellationToken cancellationToken)
        {
            var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

            var payload = new
            {
                model,
                messages = new[]
                {
                new { role = "system", content = "Bạn là trợ lý cố vấn lộ trình học tiếng Nhật, chỉ trả JSON." },
                new { role = "user", content = prompt },
            },
                response_format = new { type = "json_object" },
                temperature = 0.4,
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
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? throw new AiServiceException("Dịch vụ AI trả về nội dung rỗng.");
        }
    }
}
