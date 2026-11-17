namespace RikiPath.Domain.Entities;

/// <summary>
/// Sổ cái quyền ghi danh lớp nhóm tách biệt hoàn toàn với vé tư vấn 1-1 (MentorCreditGrant).
/// </summary>
public sealed class CohortEnrollmentEntitlement : Base
{
    public int Id { get; set; }
    public int StudyCohortId { get; set; }
    public StudyCohort StudyCohort { get; set; } = null!;

    public int? PlanId { get; set; }
    public StudyCoursePlan? Plan { get; set; }

    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;

    public string EntitlementReference { get; set; } = string.Empty;
    public string SourceType { get; set; } = "AdminManualGrant";
    public string Status { get; set; } = "Active";
    public string? Note { get; set; }

    public int? GrantedById { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
    public int? ConsumedByCourseId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedById { get; set; }
    public string? RevocationReason { get; set; }
}
