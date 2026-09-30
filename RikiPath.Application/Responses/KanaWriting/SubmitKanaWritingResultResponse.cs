namespace RikiPath.Application.Responses.KanaWriting
{
    public class SubmitKanaWritingResultResponse
    {
        public int PracticeCardId { get; set; }
        public int KanaCharacterId { get; set; }
        public int TotalMistakes { get; set; }
        public int QualityScore { get; set; }
        public int Repetitions { get; set; }
        public int IntervalDays { get; set; }
        public DateTime NextReviewDate { get; set; }
    }
}
