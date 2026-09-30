namespace RikiPath.Domain.Entities
{
    public class CertificateLevel : Base
    {
        public int Id { get; set; }

        public int CertificateTypeId { get; set; }
        public CertificateType CertificateType { get; set; }

        public string Code { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public List<UserAccount>? LearnersTargeting { get; set; }
        public List<CertificationLevelSkill>? Sections { get; set; }
        public List<Lesson>? Lessons { get; set; }
        public List<Kanji>? Kanjis { get; set; }
        public List<Vocabulary>? Vocabularies { get; set; }
        public List<GrammarPattern>? GrammarPatterns { get; set; }
        public List<MockTest>? MockTests { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
    }
}
