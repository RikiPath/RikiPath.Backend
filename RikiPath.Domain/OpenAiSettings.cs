namespace RikiPath.Domain;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string LearningPathModel { get; set; } = "gpt-4.1-mini";
    public string LearningPathReasoningEffort { get; set; } = "medium";
    public int LearningPathMaxOutputTokens { get; set; } = 25000;
    public string ContentModel { get; set; } = "gpt-4.1-mini";
    public string ReviewModel { get; set; } = "gpt-4.1-mini";
    public string GradingModel { get; set; } = "gpt-4.1";
    public string OcrModel { get; set; } = "gpt-4.1-mini";
    public string TranscriptionModel { get; set; } = "gpt-4o-mini-transcribe";
    public int TimeoutSeconds { get; set; } = 180;
    public int MaxOutputTokens { get; set; } = 8000;
}
