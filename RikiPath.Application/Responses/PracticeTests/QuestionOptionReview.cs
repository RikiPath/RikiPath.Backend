namespace RikiPath.Application.Responses.PracticeTests
{

    public class QuestionOptionReview
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
