namespace RikiPath.Application.Responses.MockTests
{

    public class QuestionOptionReview
    {
        public string? ImageUrl { get; set; }
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
