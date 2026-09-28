namespace RikiPath.Application.Responses.MockTests
{

    public class QuestionReviewCard
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<QuestionOptionReview> Options { get; set; } = new();
        public int? SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
        public bool WasPreviouslyIncorrect { get; set; }
        public int TimesAnsweredWrong { get; set; }
    }
}
