namespace RikiPath.Domain.Entities
{
    /// <summary>Join entity: which GrammarPattern a Lesson introduces. Composite key, no audit fields.</summary>
    public class LessonGrammar
    {
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; }
        public int GrammarPatternId { get; set; }
        public GrammarPattern GrammarPattern { get; set; }
    }
}
