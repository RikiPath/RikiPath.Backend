namespace RikiPath.Domain.Entities
{
    public class CertificationLevelSkill
    {
        public int CertificationLevelId { get; set; }
        public CertificationLevel CertificationLevel { get; set; }
        public int SkillId { get; set; }
        public Skill Skill { get; set; }
    }
}
