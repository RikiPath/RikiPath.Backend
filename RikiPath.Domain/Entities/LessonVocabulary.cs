namespace Domain.Entities
{
    /// <summary>Join entity: which Vocabulary a Lesson introduces. Composite key, no audit fields.</summary>
    public class LessonVocabulary
    {
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public int VocabularyEntryId { get; set; }
        public VocabularyEntry VocabularyEntry { get; set; }
    }
}
