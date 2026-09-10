namespace RikiPath.Domain.Entities
{
    /// <summary>Join entity: which GrammarPoint a Lesson introduces. Composite key, no audit fields.</summary>
    public class LessonGrammar
    {
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public int GrammarPointId { get; set; }
        public GrammarPoint GrammarPoint { get; set; }
    }
}
