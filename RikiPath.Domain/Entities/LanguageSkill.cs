using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>LanguageSkill master data (Vocabulary, Kanji, Grammar, Listening, Reading) — Admin-configurable.</summary>
    public class LanguageSkill : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public List<Lesson>? Lessons { get; set; }
        public List<MockTestSection>? MockTestSections { get; set; }
        public List<CertificationLevelSkill>? CertificationLevelSkills { get; set; }
    }
}
