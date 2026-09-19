using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    public class CertificationLevel : Base
    {
        public int Id { get; set; }

        public int CertificationId { get; set; }
        public Certification Certification { get; set; }

        public string Code { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public List<UserAccount>? LearnersTargeting { get; set; }
        public List<CertificationLevelSkill>? Sections { get; set; }
        public List<Lesson>? Lessons { get; set; }
        public List<KanjiEntry>? KanjiEntries { get; set; }
        public List<VocabularyEntry>? VocabularyEntries { get; set; }
        public List<GrammarPoint>? GrammarPoints { get; set; }
        public List<PracticeTest>? PracticeTests { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
    }
}
