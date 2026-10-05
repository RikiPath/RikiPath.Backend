namespace RikiPath.Application.Responses.KanjiWriting
{
    public class KanjiWritingScoreResponse
    {
        public int KanjiId { get; set; }
        public int Score { get; set; }
        public int CorrectStrokeCount { get; set; }
        public int TotalStrokeCount { get; set; }
        public string PracticeMode { get; set; } = "guided";
        public DateTime CompletedAt { get; set; }
    }
}
