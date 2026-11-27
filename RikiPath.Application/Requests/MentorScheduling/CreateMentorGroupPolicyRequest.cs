namespace RikiPath.Application.Requests.MentorScheduling;

public sealed class CreateMentorGroupPolicyRequest
{
    public int ExpectedCurrentVersion { get; init; }
    public int? SourceVersion { get; init; }
    public DateTime? EffectiveFromUtc { get; init; }

    // Nhóm A — Cấu trúc lớp học
    public int MaxLearners { get; init; } = 4;
    public int SessionMinutes { get; init; } = 90;

    // Nhóm B — Lịch giảng dạy
    public int RestMinutes { get; init; } = 15;
    public int MinimumPreparationDays { get; init; } = 1;

    // Nhóm C — Tuyển sinh & ghi danh
    public int MinimumPublishedEnrollmentDays { get; init; } = 7;
    public int CheckoutHoldMinutes { get; init; } = 10;

    // Nhóm D — Điều phối lớp học
    public int CancellationLeadHours { get; init; } = 24;
    public int ReschedulingLeadHours { get; init; } = 24;
    public int MentorReplacementLeadHours { get; init; } = 24;
    public bool LearnerTransferEnabled { get; init; } = true;
    public bool TransferAllowedAfterSourceStart { get; init; }
    public bool MentorReplacementEnabled { get; init; } = true;

    // Nhóm E — Đào tạo & đánh giá
    public int HomeworkDeadlineDays { get; init; } = 7;
    public int PlacementAttemptLimit { get; init; } = 2;
    public decimal RemediationCompletionThresholdPercent { get; init; } = 90m;
    public string RemediationThresholdComparison { get; init; } = ">";
    public bool RequireOfficialExamEvidence { get; init; } = true;
    public bool RequireAdminRemediationReview { get; init; } = true;
    public int KpiReportingPeriodDays { get; init; } = 30;

    // Học phí mỗi buổi theo cấp độ JLPT
    public decimal TuitionPerSessionN5 { get; init; } = 60_000m;
    public decimal TuitionPerSessionN4 { get; init; } = 75_000m;
    public decimal TuitionPerSessionN3 { get; init; } = 90_000m;
    public decimal TuitionPerSessionN2 { get; init; } = 110_000m;

    /// <summary>Khi bật, phiên bản chính sách và bảng học phí mới có hiệu lực ngay lập tức đối với các đề xuất và lớp mới.</summary>
    public bool ApplyImmediately { get; init; }
}

public sealed class UpdateMentorGroupPolicyRequest
{
    public int ExpectedRevision { get; init; }

    // Nhóm A — Cấu trúc lớp học
    public int MaxLearners { get; init; } = 4;
    public int SessionMinutes { get; init; } = 90;

    // Nhóm B — Lịch giảng dạy
    public int RestMinutes { get; init; } = 15;
    public int MinimumPreparationDays { get; init; } = 1;

    // Nhóm C — Tuyển sinh & ghi danh
    public int MinimumPublishedEnrollmentDays { get; init; } = 7;
    public int CheckoutHoldMinutes { get; init; } = 10;

    // Nhóm D — Điều phối lớp học
    public int CancellationLeadHours { get; init; } = 24;
    public int ReschedulingLeadHours { get; init; } = 24;
    public int MentorReplacementLeadHours { get; init; } = 24;
    public bool LearnerTransferEnabled { get; init; } = true;
    public bool TransferAllowedAfterSourceStart { get; init; }
    public bool MentorReplacementEnabled { get; init; } = true;

    // Nhóm E — Đào tạo & đánh giá
    public int HomeworkDeadlineDays { get; init; } = 7;
    public int PlacementAttemptLimit { get; init; } = 2;
    public decimal RemediationCompletionThresholdPercent { get; init; } = 90m;
    public string RemediationThresholdComparison { get; init; } = ">";
    public bool RequireOfficialExamEvidence { get; init; } = true;
    public bool RequireAdminRemediationReview { get; init; } = true;
    public int KpiReportingPeriodDays { get; init; } = 30;

    public decimal? TuitionPerSessionN5 { get; init; }
    public decimal? TuitionPerSessionN4 { get; init; }
    public decimal? TuitionPerSessionN3 { get; init; }
    public decimal? TuitionPerSessionN2 { get; init; }
}

public sealed class UpdateMentorGroupPolicySectionRequest
{
    public int ExpectedRevision { get; init; }

    // A. Cấu trúc lớp học (sectionKey: "structure")
    public int? MaxLearners { get; init; }
    public int? SessionMinutes { get; init; }
    public decimal? TuitionPerSessionN5 { get; init; }
    public decimal? TuitionPerSessionN4 { get; init; }
    public decimal? TuitionPerSessionN3 { get; init; }
    public decimal? TuitionPerSessionN2 { get; init; }

    // B. Lịch giảng dạy (sectionKey: "schedule")
    public int? RestMinutes { get; init; }
    public int? MinimumPreparationDays { get; init; }

    // C. Tuyển sinh & ghi danh (sectionKey: "admission")
    public int? MinimumPublishedEnrollmentDays { get; init; }
    public int? CheckoutHoldMinutes { get; init; }

    // D. Điều phối lớp học (sectionKey: "coordination")
    public int? CancellationLeadHours { get; init; }
    public int? ReschedulingLeadHours { get; init; }
    public int? MentorReplacementLeadHours { get; init; }
    public bool? LearnerTransferEnabled { get; init; }
    public bool? MentorReplacementEnabled { get; init; }

    // E. Đào tạo & đánh giá (sectionKey: "training")
    public int? HomeworkDeadlineDays { get; init; }
    public int? PlacementAttemptLimit { get; init; }
    public decimal? RemediationCompletionThresholdPercent { get; init; }
    public string? RemediationThresholdComparison { get; init; }
    public int? KpiReportingPeriodDays { get; init; }
}
