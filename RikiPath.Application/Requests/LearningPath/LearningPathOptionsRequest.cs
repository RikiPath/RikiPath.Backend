namespace RikiPath.Application.Requests.LearningPath;

public class LearningPathOptionsRequest
{
    public int? CertificateTypeId { get; set; }
    public int? TargetCertificateLevelId { get; set; }
    /// <summary>Alias retained for the JLPT frontend.</summary>
    public int? TargetJlptLevelId { get; set; }
    public int? DailyStudyMinutes { get; set; }
    public int? DailyStudyMinutesMin { get; set; }
    public int? DailyStudyMinutesMax { get; set; }
    public DateTime? TargetExamDate { get; set; }
    /// <summary>Deprecated and ignored. Horizon is computed from skill gaps and study workload.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Horizon { get; set; }
}
