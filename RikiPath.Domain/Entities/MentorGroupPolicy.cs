namespace RikiPath.Domain.Entities;

// Chính sách lớp học; lớp đã công bố giữ nguyên sức chứa, học phí và phiên bản chính sách tại thời điểm duyệt.
public sealed class MentorGroupPolicy
{
    public int Id { get; set; }
    public int Version { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsActive { get; set; }
    public bool IsArchivedRevision { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? LastAppliedAtUtc { get; set; }
    public int? LastAppliedByAdminId { get; set; }

    // Nhóm A — Cấu trúc lớp học
    public int MaxLearners { get; set; } = 4;
    public int SessionMinutes { get; set; } = 90;

    // Nhóm B — Lịch giảng dạy
    public int RestMinutes { get; set; } = 15;
    public int MinimumPreparationDays { get; set; } = 1;

    // Nhóm C — Tuyển sinh & ghi danh
    public int MinimumPublishedEnrollmentDays { get; set; } = 7;
    public int CheckoutHoldMinutes { get; set; } = 10;

    // Nhóm D — Điều phối lớp học
    public int CancellationLeadHours { get; set; } = 24;
    public int ReschedulingLeadHours { get; set; } = 24;
    public int MentorReplacementLeadHours { get; set; } = 24;
    // Chuyển lớp: luôn cần Admin duyệt. Không chuyển lớp sau khi khóa học đã bắt đầu.
    public bool LearnerTransferEnabled { get; set; } = true;
    public bool TransferAllowedAfterSourceStart { get; set; }
    public bool MentorReplacementEnabled { get; set; } = true;

    // Nhóm E — Đào tạo & đánh giá
    public int HomeworkDeadlineDays { get; set; } = 7;
    public int PlacementAttemptLimit { get; set; } = 2;
    public decimal RemediationCompletionThresholdPercent { get; set; } = 90m;
    public string RemediationThresholdComparison { get; set; } = ">";
    public bool RequireOfficialExamEvidence { get; set; } = true;
    public bool RequireAdminRemediationReview { get; set; } = true;
    public int KpiReportingPeriodDays { get; set; } = 30;

    // Đơn giá học phí trên mỗi học viên cho mỗi buổi học (VND) theo cấp độ JLPT N5–N2.
    public decimal TuitionPerSessionN5 { get; set; } = 60_000m;
    public decimal TuitionPerSessionN4 { get; set; } = 75_000m;
    public decimal TuitionPerSessionN3 { get; set; } = 90_000m;
    public decimal TuitionPerSessionN2 { get; set; } = 110_000m;

    public int? CreatedByAdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedByAdminId { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByAdminId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? CreatedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? LastAppliedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? UpdatedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? DeletedByAdmin { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CreatedByAdminName => FormatAdminName(CreatedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? LastAppliedByAdminName => FormatAdminName(LastAppliedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? UpdatedByAdminName => FormatAdminName(UpdatedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool CanDelete { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? DeleteBlockedReason { get; set; }

    private static string? FormatAdminName(UserAccount? user)
    {
        if (user is null) return null;
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email.Trim() : null);
    }

    public decimal GetTuitionPerSession(string? levelCode) => (levelCode?.Trim().ToUpperInvariant()) switch
    {
        "N5" => TuitionPerSessionN5,
        "N4" => TuitionPerSessionN4,
        "N3" => TuitionPerSessionN3,
        "N2" => TuitionPerSessionN2,
        _ => 0m,
    };

    public decimal CalculateCourseTuition(string? levelCode, int totalSessions)
    {
        if (totalSessions <= 0) return 0m;
        var perSession = GetTuitionPerSession(levelCode);
        return perSession > 0m ? decimal.Truncate(perSession) * totalSessions : 0m;
    }
}
