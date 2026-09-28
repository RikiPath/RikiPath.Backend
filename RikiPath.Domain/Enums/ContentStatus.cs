namespace RikiPath.Domain.Enums
{
    /// <summary>
    /// Content authoring workflow status, used by Course, Lesson, Kanji,
    /// Vocabulary, GrammarPattern and MockTest.
    /// </summary>
    public enum ContentStatus
    {
        Draft,
        PendingReview,
        Published,
        Rejected
    }
}
