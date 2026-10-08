namespace RikiPath.Application.Responses.SpeechToText
{
    public class SpeechTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }
}
