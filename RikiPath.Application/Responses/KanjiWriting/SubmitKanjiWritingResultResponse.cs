namespace RikiPath.Application.Responses.KanjiWriting
{
    public class SubmitKanjiWritingResultResponse
    {
        public int ReviewCardId { get; set; }
        public int TotalMistakes { get; set; }
        public int QualityScore { get; set; }
        public int Repetitions { get; set; }
        public int IntervalDays { get; set; }
        public DateTime NextReviewDate { get; set; }
        public int Score { get; set; }
    }
}
