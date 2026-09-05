using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>Skill master data (Vocabulary, Kanji, Grammar, Listening, Reading) — Admin-configurable.</summary>
    public class Skill : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public List<Lesson>? Lessons { get; set; }
        public List<PracticeTestSection>? PracticeTestSections { get; set; }
    }
}
