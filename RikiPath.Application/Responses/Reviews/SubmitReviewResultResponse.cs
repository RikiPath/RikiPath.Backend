namespace RikiPath.Application.Responses.Reviews
{
    public class SubmitReviewResultResponse
    {
        public int ReviewItemId { get; set; }
        public int QualitySubmitted { get; set; }
        public double NewEaseFactor { get; set; }
        public int NewIntervalDays { get; set; }
        public int Repetitions { get; set; }
        public DateTime NextReviewDate { get; set; }
    }
}
