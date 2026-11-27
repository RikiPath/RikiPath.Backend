namespace RikiPath.Application.Requests.MentorScheduling;

public sealed class CreateMentorGroupPolicyRequest
{
    public int ExpectedCurrentVersion { get; init; }
    public int? SourceVersion { get; init; }
    public DateTime? EffectiveFromUtc { get; init; }
    public int MaxLearners { get; init; }
    public int EnrollmentCutoffMinutesBeforeFirstSession { get; init; }
    public int SessionMinutes { get; init; }
    public int RestMinutes { get; init; }
    public int DailySessionCap { get; init; }
    // 0 = không giới hạn số buổi mỗi tuần.
    public int WeeklySessionCap { get; init; }
    public int PublicationHorizonDays { get; init; }
    public int CancellationLeadHours { get; init; }
    public int ReschedulingLeadHours { get; init; }
    public int HomeworkDeadlineDays { get; init; }
    public int PlacementAttemptLimit { get; init; }
    public decimal RemediationCompletionThresholdPercent { get; init; }
    public string RemediationThresholdComparison { get; init; } = ">";
    public bool RequireOfficialExamEvidence { get; init; } = true;
    public bool RequireAdminRemediationReview { get; init; } = true;
    public int KpiReportingPeriodDays { get; init; }
    public bool LearnerTransferEnabled { get; init; } = true;
    public bool TransferAllowedAfterSourceStart { get; init; }
    public bool MentorReplacementEnabled { get; init; } = true;
    public int MentorReplacementLeadHours { get; init; } = 24;
    public List<List<int>> RecurringWeekdayPatterns { get; init; } = [];
    public int CourseDurationWeeks { get; init; } = 10;
    // Danh sách số buổi/tuần được phép, phân tách bằng dấu phẩy (mặc định "2,3,4").
    public string AllowedSessionsPerWeekCsv { get; init; } = "2,3,4";
    public int DefaultSessionsPerWeek { get; init; } = 3;
    public int CheckoutHoldMinutes { get; init; } = 10;
    public int MinLearnersToStart { get; init; } = 1;
    public int MinimumProposalLeadDays { get; init; } = 14;
    public int MinimumPublishedEnrollmentDays { get; init; } = 7;
    public decimal TuitionPerSessionN5 { get; init; } = 60_000m;
    public decimal TuitionPerSessionN4 { get; init; } = 75_000m;
    public decimal TuitionPerSessionN3 { get; init; } = 90_000m;
    public decimal TuitionPerSessionN2 { get; init; } = 110_000m;
    public int MaxTotalActiveClassesPerMentor { get; init; } = 4;
    public Dictionary<string, int> MaxActiveClassesByLevel { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Khi bật, phiên bản chính sách và bảng học phí mới có hiệu lực ngay lập tức đối với các đề xuất và lớp mới.</summary>
    public bool ApplyImmediately { get; init; }
}

public sealed class UpdateMentorGroupPolicyRequest
{
    public int ExpectedRevision { get; init; }
    public int MaxLearners { get; init; }
    public int MinLearnersToStart { get; init; } = 1;
    public int MaxTotalActiveClassesPerMentor { get; init; } = 4;
    public Dictionary<string, int> MaxActiveClassesByLevel { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int CourseDurationWeeks { get; init; } = 10;
    public string AllowedSessionsPerWeekCsv { get; init; } = "2,3,4";
    public int DefaultSessionsPerWeek { get; init; } = 3;
    public int SessionMinutes { get; init; } = 90;
    public int RestMinutes { get; init; } = 15;
    public int DailySessionCap { get; init; } = 4;
    public int WeeklySessionCap { get; init; }
    public int PublicationHorizonDays { get; init; } = 30;
    public int MinimumProposalLeadDays { get; init; } = 14;
    public int MinimumPublishedEnrollmentDays { get; init; } = 7;
    public int EnrollmentCutoffMinutesBeforeFirstSession { get; init; }
    public int CheckoutHoldMinutes { get; init; } = 10;
    public int CancellationLeadHours { get; init; } = 24;
    public int ReschedulingLeadHours { get; init; } = 24;
    public bool LearnerTransferEnabled { get; init; } = true;
    public bool TransferAllowedAfterSourceStart { get; init; }
    public bool MentorReplacementEnabled { get; init; } = true;
    public int MentorReplacementLeadHours { get; init; } = 24;
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

    // A. Giới hạn lớp của giáo viên (sectionKey: "limits")
    public int? MaxTotalActiveClassesPerMentor { get; init; }
    public Dictionary<string, int>? MaxActiveClassesByLevel { get; init; }

    // B. Cấu trúc lớp học (sectionKey: "structure")
    public int? MinLearnersToStart { get; init; }
    public int? MaxLearners { get; init; }
    public int? CourseDurationWeeks { get; init; }
    public string? AllowedSessionsPerWeekCsv { get; init; }
    public int? DefaultSessionsPerWeek { get; init; }
    public int? SessionMinutes { get; init; }

    // C. Lịch và tải giảng dạy (sectionKey: "schedule")
    public int? RestMinutes { get; init; }
    public int? DailySessionCap { get; init; }
    public int? WeeklySessionCap { get; init; }
    public int? PublicationHorizonDays { get; init; }

    // D. Tuyển sinh & ghi danh (sectionKey: "admission")
    public int? MinimumProposalLeadDays { get; init; }
    public int? MinimumPublishedEnrollmentDays { get; init; }
    public int? EnrollmentCutoffMinutesBeforeFirstSession { get; init; }
    public int? CheckoutHoldMinutes { get; init; }

    // E. Điều phối lớp học (sectionKey: "coordination")
    public int? CancellationLeadHours { get; init; }
    public int? ReschedulingLeadHours { get; init; }
    public int? MentorReplacementLeadHours { get; init; }
    public bool? LearnerTransferEnabled { get; init; }
    public bool? MentorReplacementEnabled { get; init; }

    // F. Đào tạo & đánh giá (sectionKey: "training")
    public int? HomeworkDeadlineDays { get; init; }
    public int? PlacementAttemptLimit { get; init; }
    public decimal? RemediationCompletionThresholdPercent { get; init; }
    public string? RemediationThresholdComparison { get; init; }
    public int? KpiReportingPeriodDays { get; init; }
}
