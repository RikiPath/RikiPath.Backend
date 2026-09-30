using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>One section of a practice test, tied to a LanguageSkill (vocab/grammar, reading, listening).</summary>
    public class MockTestSection : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int SortOrder { get; set; }

        public int MockTestId { get; set; }
        public MockTest MockTest { get; set; }
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; }

        public List<MockQuestion>? Questions { get; set; }
        public List<MockTestSectionResult>? SectionResults { get; set; }
    }
}
