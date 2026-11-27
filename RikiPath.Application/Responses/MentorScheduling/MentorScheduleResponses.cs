namespace RikiPath.Application.Responses.MentorScheduling
{
    public sealed class MentorScheduleDaypartResponse
    {
        public string Code { get; init; } = string.Empty;
        public string LocalStart { get; init; } = string.Empty;
        public string LocalEnd { get; init; } = string.Empty;
    }

    public sealed class MentorSchedulePolicySummary
    {
        public int PolicyVersion { get; init; }
        public int SessionMinutes { get; init; }
        public int RestMinutes { get; init; }
        public int DailySessionCap { get; init; }
        public int WeeklySessionCap { get; init; }
        public int MaxSlotsPerSubmission { get; init; } = 14;
        public int MaxPendingSlots { get; init; } = 24;
        public int SubmissionCooldownSeconds { get; init; } = 5;
        public int PublicationHorizonDays { get; init; }
        public int CourseDurationWeeks { get; init; }
        public int CancellationLeadHours { get; init; }
        public int ReschedulingLeadHours { get; init; }
        public int MaxLearners { get; init; }
        public int GridStepMinutes { get; init; }
        public int[] AllowedGridSteps { get; init; } = [15, 30];
        public List<MentorScheduleDaypartResponse> Dayparts { get; init; } = [];
    }

    public sealed class MentorScheduleResponse
    {
        public long ScheduleId { get; init; }
        public int MentorId { get; init; }
        public string MentorName { get; init; } = string.Empty;
        public int PolicyVersion { get; init; } = 3;
        public string TimeZoneId { get; init; } = "Asia/Ho_Chi_Minh";
        public bool AutoPublish { get; init; }
        public DateOnly EffectiveFrom { get; init; }
        public DateOnly? GeneratedThrough { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public MentorSchedulePolicySummary? Policy { get; init; }
        public List<MentorScheduleRuleResponse> Rules { get; init; } = [];
    }

    public sealed class MentorScheduleRuleResponse
    {
        public long Id { get; init; }
        public long ScheduleId { get; init; }
        public int Weekday { get; init; }
        public string WeekdayName { get; init; } = string.Empty;
        public string DaypartCode { get; init; } = string.Empty;
        public string LocalStart { get; init; } = string.Empty;
        public string LocalEnd { get; init; } = string.Empty;
        public DateOnly EffectiveFrom { get; init; }
        public DateOnly? EffectiveTo { get; init; }
    }

    public sealed class SlotPreviewItemResponse
    {
        public DateOnly LocalDate { get; init; }
        public string DaypartCode { get; init; } = string.Empty;
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public string LocalStart { get; init; } = string.Empty;
        public string LocalEnd { get; init; } = string.Empty;
        public string GenerationKey { get; init; } = string.Empty;
        public bool IsExisting { get; init; }
        public bool HasConflict { get; init; }
        public string? ConflictReason { get; init; }
    }

    public sealed class SlotPreviewResponse
    {
        public int TotalCandidateSlots { get; init; }
        public int ExistingSlotsCount { get; init; }
        public int NewSlotsCount { get; init; }
        public int ConflictSlotsCount { get; init; }
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public List<SlotPreviewItemResponse> Slots { get; init; } = [];
    }

    public sealed class PublishedSlotItemResponse
    {
        public int AvailabilityId { get; init; }
        public DateOnly LocalDate { get; init; }
        public string DaypartCode { get; init; } = string.Empty;
        public DateTime StartTimeUtc { get; init; }
        public DateTime EndTimeUtc { get; init; }
        public string LocalStart { get; init; } = string.Empty;
        public string LocalEnd { get; init; } = string.Empty;
        public string GenerationKey { get; init; } = string.Empty;
        public bool IsApproved { get; init; }
    }

    public sealed class PublishSlotsResponse
    {
        public int CreatedCount { get; init; }
        public int SkippedExistingCount { get; init; }
        public int TotalSlotsProcessed { get; init; }
        public DateOnly GeneratedThrough { get; init; }
        public List<PublishedSlotItemResponse> PublishedSlots { get; init; } = [];
    }
}
