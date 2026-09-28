namespace RikiPath.Application.Responses.MockTests
{
    public class DetailedAttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public double TotalScore { get; set; }
        public List<QuestionReviewCard> Questions { get; set; } = [];
    }
}
