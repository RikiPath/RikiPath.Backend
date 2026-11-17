namespace RikiPath.Application.Requests.MentorScheduling;

public sealed class CreateMentorGroupPolicyRequest
{
    public int ExpectedCurrentVersion { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
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
}
