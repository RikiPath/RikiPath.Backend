using RikiPath.Application.Requests.MentorScheduling;

namespace RikiPath.Application.Responses.MentorScheduling;

public sealed record SupportedPolicyLevelDto(int Id, string Code, string Name);

public sealed class TeachingProposalPolicyResponse
{
    public int PolicyId { get; set; }
    public int PolicyVersion { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int MinSessionsPerWeek { get; set; } = 2;
    public int MaxSessionsPerWeek { get; set; } = 4;
    public int DefaultSessionsPerWeek { get; set; } = 3;
    public List<int> AllowedSessionsPerWeek { get; set; } = [2, 3, 4];
    public int CourseDurationWeeks { get; set; } = 10;
    public int SessionDurationMinutes { get; set; } = 90;
    public int RestMinutes { get; set; } = 15;
    public int MinLearners { get; set; } = 1;
    public int MaxLearners { get; set; } = 4;
    public int DailySessionCap { get; set; } = 4;
    public int WeeklySessionCap { get; set; }
    public int GridStepMinutes { get; set; } = 15;
    public List<int> AllowedGridSteps { get; set; } = [15, 30];
    public int OperatingStartMinute { get; set; } = 7 * 60;
    public int OperatingEndMinute { get; set; } = 22 * 60;
    public int MinimumProposalLeadDays { get; set; } = 14;
    public int MinimumPublishedEnrollmentDays { get; set; } = 7;
    public int CheckoutHoldMinutes { get; set; } = 10;
    public int EnrollmentCutoffMinutesBeforeFirstSession { get; set; }
    public int MaxTotalActiveClassesPerMentor { get; set; } = 4;
    public Dictionary<string, int> MaxActiveClassesByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> TuitionPerSessionByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class QualifiedJlptLevelItem
{
    public int CertificateLevelId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? VerifiedAtUtc { get; set; }
    public bool IsRecommendedByProfile { get; set; }
    public bool IsQualified { get; set; } = true;
    public string? QualificationReason { get; set; }
    public bool IsAvailableForNewProposal { get; set; } = true;
    public string? UnavailableReason { get; set; }
    public string? BlockingStatus { get; set; }
    public int? BlockingProposalId { get; set; }
    public int? BlockingCohortId { get; set; }
    public string? BlockingTitle { get; set; }
    public int ActiveCount { get; set; }
    public int MaxActiveClasses { get; set; } = 1;
    public int RemainingSlots { get; set; }
}

public sealed class EligibleCoursePlanItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CertificateLevelId { get; set; }
    public string LevelCode { get; set; } = string.Empty;
    public int SessionsPerWeek { get; set; }
    public int SessionMinutes { get; set; }
    public int TotalSessions { get; set; }
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class MentorWorkloadSummaryResponse
{
    public int ActiveCohortsCount { get; set; }
    public int PendingProposalsCount { get; set; }
    public int ChangesRequestedProposalsCount { get; set; }
    public int ApprovedProposalsCount { get; set; }
    public int DraftProposalsCount { get; set; }
    public int WeeklyCommittedSessions { get; set; }
    public int WeeklyPendingProposalSessions { get; set; }
    public int WeeklyTotalProjectedSessions { get; set; }
    public int PeakDaySessions { get; set; }
    public int DailySessionCap { get; set; }
    public int WeeklySessionCap { get; set; }
}

public sealed class ProposalValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error"; // Error | Warning
    public string Message { get; set; } = string.Empty;
    public int? Weekday { get; set; }
    public int? StartMinute { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }
    public DateTime? OccurrenceEndUtc { get; set; }
    public string? ConflictSource { get; set; }
    public string? ConflictDate { get; set; }
    public string? ConflictLocalStart { get; set; }
    public string? ConflictLocalEnd { get; set; }
    public string? ConflictJlptLevel { get; set; }
    public string? ConflictStatus { get; set; }
    public string? ConflictEntityType { get; set; }
    public int? ConflictProposalId { get; set; }
    public int? ConflictCohortId { get; set; }
}

public sealed class GeneratedSessionOccurrenceResponse
{
    public int SessionNumber { get; set; }
    public int WeekNumber { get; set; }
    public int Weekday { get; set; }
    public string LocalDate { get; set; } = string.Empty;
    public string LocalStart { get; set; } = string.Empty;
    public string LocalEnd { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}

public sealed class TeachingProposalValidationResponse
{
    public bool IsValid { get; set; }
    public bool CanSubmit { get; set; }
    public int WeeklySessionCount { get; set; }
    public int TotalSessions { get; set; }
    public int TotalTeachingMinutes { get; set; }
    public decimal TuitionPerSession { get; set; }
    public decimal TotalCourseTuition { get; set; }
    public int? TuitionPolicyVersion { get; set; }
    public DateOnly? ProjectedEndDate { get; set; }
    public List<ProposalValidationIssue> Issues { get; set; } = [];
    public List<GeneratedSessionOccurrenceResponse> OccurrencesPreview { get; set; } = [];
    public MentorWorkloadSummaryResponse Workload { get; set; } = new();
}

public sealed class TeachingProposalEventResponse
{
    public long Id { get; set; }
    public int ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public int RevisionAfter { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class TeachingProposalResponse
{
    public int Id { get; set; }
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string MentorEmail { get; set; } = string.Empty;
    public bool MentorVerified { get; set; }
    public string? MentorVerificationStatus { get; set; }
    public string? MentorJlptLevel { get; set; }
    public int? MentorYearsOfExperience { get; set; }
    public string? MentorExpertise { get; set; }
    public string? MentorWorkplace { get; set; }
    public bool MentorHasVerificationDocument { get; set; }
    public List<string> MentorQualifiedLevels { get; set; } = [];

    public int? CertificateLevelId { get; set; }
    public string? JlptLevelCode { get; set; }
    public string ProgramType { get; set; } = string.Empty;
    public int? CoursePlanId { get; set; }
    public string? CoursePlanName { get; set; }
    public string? CourseTitle { get; set; }
    public string? CourseObjective { get; set; }

    public DateOnly? ExpectedStartDate { get; set; }
    public DateOnly? ProjectedEndDate { get; set; }
    public int CourseDurationWeeks { get; set; }
    public int SessionDurationMinutes { get; set; }
    public int WeeklySessionCount { get; set; }
    public int TotalSessions { get; set; }
    public decimal TuitionPerSession { get; set; }
    public decimal TotalCourseTuition { get; set; }
    public int? TuitionPolicyVersion { get; set; }

    public List<WeeklySessionSlotDto> WeeklySessions { get; set; } = [];
    public string? OptionalNotes { get; set; }
    public List<ProposalAttachmentDto> OptionalAttachments { get; set; } = [];

    public string Status { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? ReviewedByAdminName { get; set; }
    public string? ReviewReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime LastSavedAtUtc { get; set; }

    public List<int> GeneratedAvailabilityIds { get; set; } = [];
    public int? LinkedCohortId { get; set; }
    public string? LinkedCohortName { get; set; }
    public string? LinkedCohortStatus { get; set; }

    public bool HasConflicts { get; set; }
    public List<ProposalValidationIssue> ValidationIssues { get; set; } = [];
    public List<GeneratedSessionOccurrenceResponse> OccurrencesPreview { get; set; } = [];
    public MentorWorkloadSummaryResponse? MentorWorkload { get; set; }
    public List<TeachingProposalEventResponse> History { get; set; } = [];
}

public sealed class MentorTeachingProposalContextResponse
{
    public bool MentorVerified { get; set; }
    public string? MentorVerificationStatus { get; set; }
    public string? MentorDeclaredJlptLevel { get; set; }
    public string? MentorVerifiedJlptLevel { get; set; }
    public TeachingProposalPolicyResponse Policy { get; set; } = new();
    public List<QualifiedJlptLevelItem> AvailableLevels { get; set; } = [];
    public List<QualifiedJlptLevelItem> QualifiedLevels { get; set; } = [];
    public List<EligibleCoursePlanItem> CoursePlans { get; set; } = [];
    public MentorWorkloadSummaryResponse Workload { get; set; } = new();
    public List<TeachingProposalResponse> Proposals { get; set; } = [];
}

public sealed class BulkApproveProposalItemResult
{
    public int ProposalId { get; set; }
    public int? MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string? JlptLevelCode { get; set; }
    public string? CourseTitle { get; set; }
    public DateOnly? ExpectedStartDate { get; set; }
    public int WeeklySessionCount { get; set; }
    public int TotalSessions { get; set; }
    public decimal TuitionPerSession { get; set; }
    public decimal TotalCourseTuition { get; set; }
    public int? TuitionPolicyVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? CurrentRevision { get; set; }
    public bool IsEligible { get; set; }
    public bool Approved { get; set; }
    public int? LinkedCohortId { get; set; }
    public string? LinkedCohortName { get; set; }
    public List<int> GeneratedAvailabilityIds { get; set; } = [];
    public List<ProposalValidationIssue> Issues { get; set; } = [];
}

public sealed class RepairApprovedCohortsResponse
{
    public int ScannedCount { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<int> CreatedCohortIds { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class BulkApproveTeachingProposalsResponse
{
    public bool AllEligible { get; set; }
    public bool Executed { get; set; }
    public int TotalRequested { get; set; }
    public int EligibleCount { get; set; }
    public int InvalidCount { get; set; }
    public int ApprovedCount { get; set; }
    public List<int> ApprovedProposalIds { get; set; } = [];
    public List<BulkApproveProposalItemResult> Results { get; set; } = [];
}

public sealed class RepairedProposalScheduleItem
{
    public int ProposalId { get; set; }
    public string? CourseTitle { get; set; }
    public string? JlptLevelCode { get; set; }
    public string Action { get; set; } = string.Empty;
    public int PreviousWeeklyCount { get; set; }
    public int RestoredWeeklyCount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RepairInvalidProposalSchedulesResponse
{
    public int ScannedCount { get; set; }
    public int InvalidFoundCount { get; set; }
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
    public List<RepairedProposalScheduleItem> Items { get; set; } = [];
}
