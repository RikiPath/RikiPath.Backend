namespace RikiPath.Domain.Entities;

public sealed class MentorGroupPolicyLevelLimit
{
    public int Id { get; set; }
    public int MentorGroupPolicyId { get; set; }
    public int CertificateLevelId { get; set; }
    public int MaxActiveClasses { get; set; } = 1;

    [System.Text.Json.Serialization.JsonIgnore]
    public CertificateLevel? CertificateLevel { get; set; }
}
