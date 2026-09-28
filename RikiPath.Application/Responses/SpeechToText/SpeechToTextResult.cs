namespace RikiPath.Application.Responses.SpeechToText
{
    public class SpeechToTextResult(string transcribedText, double confidence, string rawResponseJson)
    {
        public string TranscribedText { get; init; }
        public double Confidence { get; init; }
        public string RawResponseJson { get; init; }
    }
}
