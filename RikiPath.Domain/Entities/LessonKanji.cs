namespace RikiPath.Domain.Entities
{
    /// <summary>Join entity: which Kanji a Lesson introduces. Composite key, no audit fields.</summary>
    public class LessonKanji
    {
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public int KanjiId { get; set; }
        public Kanji Kanji { get; set; }
    }
}
