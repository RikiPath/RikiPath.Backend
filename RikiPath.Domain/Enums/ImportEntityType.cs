namespace RikiPath.Domain.Enums
{
    /// <summary>Loại nội dung mà 1 sheet Excel ánh xạ tới - dùng chung cho cả Bulk và Single upload.</summary>
    public enum ImportEntityType
    {
        Kanji,
        Vocabulary,
        Grammar,
        Lesson,
        MockTest,
        PracticeExercise,
    }
}
