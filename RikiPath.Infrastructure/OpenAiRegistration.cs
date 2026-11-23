using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RikiPath.Application.IClients;
using RikiPath.Domain;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Infrastructure;

public static class OpenAiRegistration
{
    public static IServiceCollection AddOpenAiFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiSettings>(settings =>
        {
            configuration.GetSection("AppSettings:OpenAI").Bind(settings);
            configuration.GetSection("OpenAI").Bind(settings);
            var key = configuration["OPENAI_API_KEY"];
            if (!string.IsNullOrWhiteSpace(key)) settings.ApiKey = key;
        });
        services.AddHttpClient<OpenAiClient>(http => http.Timeout = Timeout.InfiniteTimeSpan);
        services.AddScoped<IAiLearningPathClient, OpenAiLearningPathClient>();
        services.AddScoped<IAiGradingClient, OpenAiGradingClient>();
        services.AddScoped<IAiMockTestExplanationClient, OpenAiMockTestExplanationClient>();
        services.AddScoped<IAiContentAuthoringClient, OpenAiContentAuthoringClient>();
        services.AddScoped<IAiEssayGradingClient, OpenAiEssayGradingClient>();
        services.AddScoped<IAiContentReviewClient, OpenAiContentReviewClient>();
        services.AddScoped<IAiOcrClient, OpenAiOcrClient>();
        services.AddScoped<IAiTranslationClient, OpenAiTranslationClient>();
        services.AddScoped<ISpeechRecognitionClient, AwsAudioTranscriptionClient>();
        return services;
    }
}
