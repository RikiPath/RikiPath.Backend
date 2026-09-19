namespace RikiPath.Application.Responses.PracticeTests
{
    public class DetailedAttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int PracticeTestId { get; set; }
        public double TotalScore { get; set; }
        public List<QuestionReviewItem> Questions { get; set; } = [];
    }
}
