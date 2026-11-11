namespace RikiPath.Application.Responses.SpeechToText
{
    public class SpeechToTextResult(string transcribedText, double confidence, string rawResponseJson)
    {
        public string TranscribedText { get; init; } = transcribedText;
        public double Confidence { get; init; } = confidence;
        public string RawResponseJson { get; init; } = rawResponseJson;
    }
}
