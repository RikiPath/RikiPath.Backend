namespace RikiPath.Application.Responses.MockTests
{

    public class QuestionOptionReview
    {
        public string? ImageUrl { get; set; }
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        /// <summary>Giải thích do content author cung cấp, dùng khi AI tạo phản hồi sau bài thi.</summary>
        public string? Explanation { get; set; }
        public bool IsCorrect { get; set; }
    }
}
