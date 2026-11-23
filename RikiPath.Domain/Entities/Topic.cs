namespace RikiPath.Domain.Entities;

public class Topic : Base
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int? CertificateLevelId { get; set; }
    public CertificateLevel? CertificateLevel { get; set; }
    public string? StageCode { get; set; }
    public string? LearningObjectives { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
}
