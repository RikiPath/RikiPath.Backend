namespace Domain.Enums
{
    /// <summary>
    /// Content authoring workflow status, used by Course, Lesson, KanjiEntry,
    /// VocabularyEntry, GrammarPoint and PracticeTest.
    /// </summary>
    public enum ContentStatus
    {
        Draft,
        PendingReview,
        Published,
        Rejected
    }
}
