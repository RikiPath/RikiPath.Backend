namespace RikiPath.Application.Responses.PracticeTests
{
    public class TestSummaryResponse
    {
        public int PracticeTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
    }

    public class QuestionOptionReview
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class QuestionReviewItem
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<QuestionOptionReview> Options { get; set; } = new();
        public int? SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
    }

    public class DetailedAttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int PracticeTestId { get; set; }
        public double TotalScore { get; set; }
        public List<QuestionReviewItem> Questions { get; set; } = new();
    }
}
