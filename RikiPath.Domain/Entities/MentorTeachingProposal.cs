namespace RikiPath.Domain.Entities;

public static class MentorTeachingProposalStatus
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string ChangesRequested = "ChangesRequested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static readonly string[] All =
    [
        Draft,
        PendingReview,
        ChangesRequested,
        Approved,
        Rejected,
        Withdrawn,
    ];
}

public static class MentorTeachingProgramType
{
    public const string StandardJlpt = "StandardJlpt";
    public const string IntensiveJlpt = "IntensiveJlpt";
    public const string ExamPrepJlpt = "ExamPrepJlpt";
    public const string CustomJlpt = "CustomJlpt";

    public static readonly string[] All =
    [
        StandardJlpt,
        IntensiveJlpt,
        ExamPrepJlpt,
        CustomJlpt,
    ];
}

/// <summary>
/// Đề xuất mở lớp học nhóm JLPT do Giáo viên soạn thảo và gửi duyệt.
/// Tách biệt rõ ràng với khung giờ rảnh lịch sử (MentorAvailability) và lớp nhóm đã công bố (StudyCohort).
/// </summary>
public sealed class MentorTeachingProposal
{
    public int Id { get; set; }
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;

    public int? CertificateLevelId { get; set; }
    public CertificateLevel? CertificateLevel { get; set; }

    public string ProgramType { get; set; } = MentorTeachingProgramType.StandardJlpt;
    public int? CoursePlanId { get; set; }
    public StudyCoursePlan? CoursePlan { get; set; }

    public string? CourseTitle { get; set; }
    public string? CourseObjective { get; set; }

    public DateOnly? ExpectedStartDate { get; set; }
    public int CourseDurationWeeks { get; set; } = 10;
    public int SessionDurationMinutes { get; set; } = 90;

    /// <summary>
    /// JSON mảng khung buổi lặp hàng tuần: [{ weekday: 0..6 (0=CN..6=T7), startMinute: 420..1230, endMinute: startMinute+90 }].
    /// </summary>
    public string WeeklySessionsJson { get; set; } = "[]";

    public string? OptionalNotes { get; set; }

    /// <summary>
    /// JSON mảng tệp đính kèm không bắt buộc: [{ name, url, storedFileName?, contentType?, sizeBytes? }].
    /// Không bao giờ bắt buộc để gửi duyệt hoặc phê duyệt.
    /// </summary>
    public string OptionalAttachmentsJson { get; set; } = "[]";

    public string Status { get; set; } = MentorTeachingProposalStatus.Draft;
    public int Revision { get; set; } = 1;

    public string? SubmissionIdempotencyKey { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public UserAccount? ReviewedByAdmin { get; set; }
    public string? ReviewReason { get; set; }

    public int? GroupPolicyId { get; set; }
    public MentorGroupPolicy? GroupPolicy { get; set; }
    public int? PolicyVersionSnapshot { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSavedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MentorTeachingProposalEvent> Events { get; set; } = [];
    public List<MentorAvailability> GeneratedAvailabilities { get; set; } = [];
}

public sealed class MentorTeachingProposalEvent
{
    public long Id { get; set; }
    public int ProposalId { get; set; }
    public MentorTeachingProposal Proposal { get; set; } = null!;
    public int ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public int RevisionAfter { get; set; }
    public string? Reason { get; set; }
    public string? SnapshotJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
