using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>JLPT level master data (N5..N1) — Admin-configurable.</summary>
    public class JlptLevel : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }          // e.g. "N5", "N4", "N3"
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public List<UserAccount>? LearnersTargeting { get; set; }
        public List<Course>? Courses { get; set; }
        public List<KanjiEntry>? KanjiEntries { get; set; }
        public List<VocabularyEntry>? VocabularyEntries { get; set; }
        public List<GrammarPoint>? GrammarPoints { get; set; }
        public List<PracticeTest>? PracticeTests { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
    }
}
