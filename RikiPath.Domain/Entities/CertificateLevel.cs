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
        public List<Lesson>? Lessons { get; set; }
        public List<MockTest>? MockTests { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
        public List<ContentLevelMapping>? ContentLevelMappings { get; set; }
    }
}