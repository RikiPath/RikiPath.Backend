namespace RikiPath.Infrastructure.Options;

public sealed class AmazonTranscribeSettings
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string Region { get; set; } = "ap-southeast-1";
    public string Language { get; set; } = "ja-JP";
    public int SampleRate { get; set; } = 16000;
    public int SessionUrlLifetimeSeconds { get; set; } = 60;
    public string AudioBucket { get; set; } = string.Empty;
    public string AudioPrefix { get; set; } = "rikipath-speech/";
    public int JobTimeoutSeconds { get; set; } = 120;
}
