namespace RikiPath.Domain;

public sealed class OpenRouterSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b:free";
    public int TimeoutSeconds { get; set; } = 22;
    public int MaxOutputTokens { get; set; } = 1000;
}

public sealed class GeminiSettings
{
    public List<string> ApiKey { get; set; } = [];
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent";
    public int TimeoutSeconds { get; set; } = 18;
}

public sealed class TranslationSettings
{
    public int MaxTextLength { get; set; } = 500;
    public int DailyRequestsPerUser { get; set; } = 200;
    public int DailyTokensPerUser { get; set; } = 250000;
    public int CacheHours { get; set; } = 24;
    public int ProviderTimeoutSeconds { get; set; } = 20;
}
