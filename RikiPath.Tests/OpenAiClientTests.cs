using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Domain;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Tests;

public class OpenAiClientTests
{
    [Fact]
    public async Task Learning_path_passes_configured_reasoning_and_token_budget_to_reasoning_model()
    {
        var settings = new OpenAiSettings { ApiKey = "test-key", LearningPathModel = "gpt-6.1-sol",
            LearningPathReasoningEffort = "high", LearningPathMaxOutputTokens = 25000 };
        var handler = new Handler(async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("gpt-6.1-sol", payload.RootElement.GetProperty("model").GetString());
            Assert.Equal("high", payload.RootElement.GetProperty("reasoning_effort").GetString());
            Assert.Equal(25000, payload.RootElement.GetProperty("max_completion_tokens").GetInt32());
            Assert.False(payload.RootElement.TryGetProperty("temperature", out _));
            return Completion("{}", 123);
        });
        var client = new OpenAiLearningPathClient(new OpenAiClient(new HttpClient(handler), Options.Create(settings)), Mock.Of<IAiUsageQuotaService>());
        Assert.Equal("{}", await client.GenerateLearningPathJsonAsync(7, "JSON", default));
    }

    [Fact]
    public async Task Grading_uses_OpenAI_json_contract_and_records_actual_usage()
    {
        var quota = new Mock<IAiUsageQuotaService>();
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("gpt-4.1", payload.RootElement.GetProperty("model").GetString());
            Assert.Equal("json_object", payload.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            Assert.False(payload.RootElement.TryGetProperty("reasoning", out _));
            return Completion("{\"score\":80}", 123);
        });
        var client = new OpenAiGradingClient(Create(handler), quota.Object);
        Assert.Equal("{\"score\":80}", await client.GradeSubmissionJsonAsync(7, "Grade as JSON", default));
        quota.Verify(x => x.EnsureWithinQuotaAsync(7, AiFeature.Grading, default), Times.Once);
        quota.Verify(x => x.RecordUsageAsync(7, AiFeature.Grading, 123, default), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Provider_errors_are_not_swallowed(HttpStatusCode status)
    {
        var client = Create(new Handler(_ => Task.FromResult(new HttpResponseMessage(status))));
        if (status == HttpStatusCode.TooManyRequests)
            await Assert.ThrowsAsync<AiQuotaExceededException>(() => client.CompleteAsync("model", "JSON", "prompt", true, default));
        else
            await Assert.ThrowsAsync<AiServiceException>(() => client.CompleteAsync("model", "JSON", "prompt", true, default));
    }

    [Theory]
    [InlineData("not JSON", "stop")]
    [InlineData("{}", "length")]
    public async Task Rejects_invalid_or_truncated_JSON(string content, string finish)
    {
        var client = Create(new Handler(_ => Task.FromResult(Completion(content, 5, finish))));
        await Assert.ThrowsAsync<AiServiceException>(() => client.CompleteAsync("model", "JSON", "prompt", true, default));
    }

    [Fact]
    public async Task OCR_sends_image_as_base64_data_url()
    {
        var client = new OpenAiOcrClient(Create(new Handler(async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var parts = payload.RootElement.GetProperty("messages")[1].GetProperty("content");
            Assert.Equal("data:image/png;base64,aGVsbG8=", parts[1].GetProperty("image_url").GetProperty("url").GetString());
            Assert.False(payload.RootElement.TryGetProperty("response_format", out _));
            return Completion("日本語", 20);
        })));
        Assert.Equal("日本語", await client.ScanHandwritingAsync("Read handwriting", "aGVsbG8=", "image/png"));
    }

    [Fact]
    public async Task Transcription_sends_multipart_audio_and_returns_text()
    {
        var client = new OpenAiSpeechToTextClient(Create(new Handler(async request =>
        {
            Assert.Equal("/v1/audio/transcriptions", request.RequestUri!.AbsolutePath);
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var body = await form.ReadAsStringAsync();
            Assert.Contains("gpt-4o-mini-transcribe", body);
            Assert.Contains("audio.wav", body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { text = "こんにちは" }) };
        })), Mock.Of<IHttpClientFactory>());
        using var audio = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.Equal("こんにちは", await client.TranscribeAsync(audio, "voice.wav", default));
    }

    [Fact]
    public void Registration_routes_every_feature_to_OpenAI_and_ignores_legacy_provider_settings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:LearningPathProvider"] = "Gemini", ["OpenRouter:ApiKey"] = "legacy",
            ["OpenAI:ApiKey"] = "test-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAiUsageQuotaService>());
        services.AddOpenAiFeatures(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<OpenAiLearningPathClient>(scope.ServiceProvider.GetRequiredService<IAiLearningPathClient>());
        Assert.IsType<OpenAiGradingClient>(scope.ServiceProvider.GetRequiredService<IAiGradingClient>());
        Assert.IsType<OpenAiContentAuthoringClient>(scope.ServiceProvider.GetRequiredService<IAiContentAuthoringClient>());
        Assert.IsType<OpenAiEssayGradingClient>(scope.ServiceProvider.GetRequiredService<IAiEssayGradingClient>());
        Assert.IsType<OpenAiContentReviewClient>(scope.ServiceProvider.GetRequiredService<IAiContentReviewClient>());
        Assert.IsType<OpenAiOcrClient>(scope.ServiceProvider.GetRequiredService<IAiOcrClient>());
        Assert.IsType<OpenAiSpeechToTextClient>(scope.ServiceProvider.GetRequiredService<ISpeechRecognitionClient>());
    }

    private static OpenAiClient Create(HttpMessageHandler handler)
        => new(new HttpClient(handler), Options.Create(new OpenAiSettings { ApiKey = "test-key" }));

    private static HttpResponseMessage Completion(string text, int tokens, string finish = "stop")
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                choices = new[] { new { message = new { content = text }, finish_reason = finish } },
                usage = new { total_tokens = tokens }
            })
        };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => callback(request);
    }
}
