namespace RikiPath.Domain.Enums
{
    /// <summary>Loại nội dung do Content Author tạo mà AI có thể kiểm tra.</summary>
    public enum AiReviewContentType
    {
        Lesson = 1,
        Kanji = 2,
        Vocabulary = 3,
        Grammar = 4,
        MockTest = 5,
        PracticeExercise = 6
    }
}