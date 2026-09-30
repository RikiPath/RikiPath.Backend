namespace RikiPath.Application.Requests.KanaWriting
{
    /// <summary>Kết quả luyện một ký tự Kana.</summary>
    public class SubmitKanaWritingResultRequest
    {
        public int KanaCharacterId { get; set; }
        public int TotalMistakes { get; set; }
    }
}
