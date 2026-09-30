namespace RikiPath.Domain.Entities
{
    public class CertificationLevelSkill
    {
        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; }
    }
}
