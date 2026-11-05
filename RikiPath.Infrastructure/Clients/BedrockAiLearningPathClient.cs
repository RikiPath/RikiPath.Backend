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

/// <summary>Amazon Bedrock Converse client for AI learning path generation.</summary>
public sealed class BedrockAiLearningPathClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IOptions<AwsSettings> awsOptions)
    : IAiLearningPathClient
{
    private const string SystemPrompt = """
        You are a JLPT learning-path advisor. Use only the learner assessment, progress, and lesson catalog supplied in the user message. Do not use external information or invent lesson names or IDs. Return only one valid JSON object with keys: summary, strengths, weaknesses, feasibility, isExamDateRealistic, recommendedStudyDays, focusTopics, dailySchedules. Every lesson ID must come from the supplied database catalog. Build a realistic progression from the learner's assessed level toward the target level, in ascending JLPT order. Write concise Vietnamese and keep each day's title and focus short so the full schedule fits in one response.
        """;

    public async Task<string> GenerateLearningPathJsonAsync(
        int userId, string prompt, CancellationToken cancellationToken)
    {
        var aws = awsOptions.Value;
        var token = configuration["AWS_BEARER_TOKEN_BEDROCK"] ??
                    configuration["AWS:ApiKey"] ??
                    aws.ApiKey;
        if (string.IsNullOrWhiteSpace(token))
            throw new AiServiceException(
                "Thiếu AWS Bedrock API key. Hãy cấu hình biến AWS_BEARER_TOKEN_BEDROCK trên máy chạy backend.");

        var region = configuration["AWS_REGION"] ?? configuration["AWS:Region"] ?? aws.Region;
        var modelId = configuration["AWS_BEDROCK_MODEL_ID"] ??
                      configuration["AWS:LearningPathModelId"] ??
                      aws.LearningPathModelId;

        var endpoint = $"https://bedrock-runtime.{region}.amazonaws.com/model/{Uri.EscapeDataString(modelId)}/converse";
        var payload = new
        {
            system = new[] { new { text = SystemPrompt } },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[] { new { text = prompt } },
                }
            },
            inferenceConfig = new
            {
                maxTokens = 20000,
                temperature = 0.1,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException("Amazon Bedrock phản hồi quá thời gian chờ 120 giây.");
        }

        using (response)
        {
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new AiQuotaExceededException("Amazon Bedrock đang giới hạn lượt gọi. Vui lòng thử lại sau.");

            throw new AiServiceException(
                $"Amazon Bedrock lỗi {(int)response.StatusCode} tại region '{region}' với model '{modelId}': {body}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("output")
                .GetProperty("message")
                .GetProperty("content");
            var text = string.Join("\n", content.EnumerateArray()
                .Where(item => item.TryGetProperty("text", out _))
                .Select(item => item.GetProperty("text").GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(text)
                ? throw new AiServiceException("Amazon Bedrock không trả về nội dung lộ trình.")
                : text;
        }
        catch (JsonException ex)
        {
            throw new AiServiceException("Không đọc được phản hồi JSON từ Amazon Bedrock.", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new AiServiceException("Phản hồi Amazon Bedrock thiếu nội dung lộ trình.", ex);
        }
        }
    }
}
