namespace RikiPath.Application.Responses.MockTests
{

    public class QuestionReviewCard
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        public System.Text.Json.JsonElement? Response { get; set; }
        public System.Text.Json.JsonElement? AnswerKey { get; set; }
        public List<QuestionOptionReview> Options { get; set; } = new();
        public int? SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
        public bool WasPreviouslyIncorrect { get; set; }
        public int TimesAnsweredWrong { get; set; }
    }
}
