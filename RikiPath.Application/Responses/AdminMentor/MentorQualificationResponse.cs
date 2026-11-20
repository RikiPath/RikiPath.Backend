namespace RikiPath.Application.Responses.AdminMentor;

public sealed class MentorQualificationLevelOptionResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string CertificateCode { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed class MentorQualificationBadgeResponse
{
    public int CertificateLevelId { get; init; }
    public string LevelCode { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime VerifiedAtUtc { get; init; }
    public string VerifiedByAdminName { get; init; } = string.Empty;
    public DateTime? RevokedAtUtc { get; init; }
}

public sealed class MentorQualificationSummaryResponse
{
    public int MentorId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string VerificationStatus { get; init; } = string.Empty;
    public string? DeclaredJlptLevel { get; init; }
    public IReadOnlyList<MentorQualificationBadgeResponse> ActiveQualifications { get; init; } = [];
    public int RevokedCount { get; init; }
}

public sealed class MentorQualificationsWorkspaceResponse
{
    public IReadOnlyList<MentorQualificationLevelOptionResponse> Levels { get; init; } = [];
    public IReadOnlyList<MentorQualificationSummaryResponse> Mentors { get; init; } = [];
}
