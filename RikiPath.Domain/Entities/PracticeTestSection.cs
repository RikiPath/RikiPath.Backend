using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>One section of a practice test, tied to a Skill (vocab/grammar, reading, listening).</summary>
    public class PracticeTestSection : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int SortOrder { get; set; }

        public int PracticeTestId { get; set; }
        public PracticeTest PracticeTest { get; set; }
        public int SkillId { get; set; }
        public Skill Skill { get; set; }

        public List<PracticeQuestion>? Questions { get; set; }
        public List<PracticeTestSectionResult>? SectionResults { get; set; }
    }
}
