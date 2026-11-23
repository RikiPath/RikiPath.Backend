using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Reviews;

public sealed class SrsForecastDayResponse
{
    public DateOnly Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int DueCount { get; set; }
    public int DueNowCount { get; set; }
    public int DueLaterCount { get; set; }
    public int ReviewedCount { get; set; }
    public DateTime? EarliestDueAtUtc { get; set; }
    public string Status { get; set; } = "None";
}

public sealed class SrsSummaryResponse
{
    public int TotalCards { get; set; }
    public int ActiveCards { get; set; }
    public int SuspendedCards { get; set; }
    public int ArchivedCards { get; set; }
    public int DueTodayCount { get; set; }
    public int TotalDueCards { get; set; }
    public int RawDueNowCount { get; set; }
    public int DueLaterTodayCount { get; set; }
    public int NewCount { get; set; }
    public int LearningCount { get; set; }
    public int RelearningCount { get; set; }
    public int ReviewCount { get; set; }
    public int MasteredCount { get; set; }

    public int ReviewedTodayCount { get; set; }
    public int CorrectTodayCount { get; set; }
    public double TodayAccuracyPercent { get; set; }
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public DateOnly StudyTodayDate { get; set; }
    public DateTime ServerUtcNow { get; set; }
    public string TimezoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public string PreferredReviewMode { get; set; } = "flip";
    public int ReminderHour { get; set; } = 20;
    public int ReminderMinute { get; set; }
    public bool DailyReminderEnabled { get; set; } = true;
    public bool HasActiveSession { get; set; }
    public Guid? ActiveSessionKey { get; set; }
    public string? ActiveSessionStudyMode { get; set; }
    public int ActiveSessionPendingCount { get; set; }
    public bool HasActivePracticeSession { get; set; }
    public Guid? ActivePracticeSessionKey { get; set; }
    public string? ActivePracticeSessionStudyMode { get; set; }
    public int ActivePracticeSessionPendingCount { get; set; }
    public int DailyNewLimit { get; set; }
    public int DailyReviewLimit { get; set; }
    public List<SrsForecastDayResponse> ForecastNext7Days { get; set; } = [];
    public List<SrsCalendarDayResponse> CalendarDays { get; set; } = [];
}

public sealed class SrsCardLibraryResponse
{
    public int TotalCount { get; set; }
    public List<ReviewQueueItemResponse> Items { get; set; } = [];
}

public sealed class SrsReviewHistoryItemResponse
{
    public int Id { get; set; }
    public int ReviewCardId { get; set; }
    public ReviewRating Rating { get; set; }
    public int Quality { get; set; }
    public string StudyMode { get; set; } = "flip";
    public string? UserAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public int? ResponseTimeMs { get; set; }
    public int PreviousIntervalDays { get; set; }
    public int NewIntervalDays { get; set; }
    public double PreviousEaseFactor { get; set; }
    public double NewEaseFactor { get; set; }
    public SrsLearningState PreviousState { get; set; }
    public SrsLearningState NewState { get; set; }
    public DateTime ReviewedAt { get; set; }
}

public sealed class SrsEnrollOutcomeResponse
{
    public string Action { get; set; } = "Created"; // Created | LinkedSource | Reactivated | AlreadyExists | Preview
    public ReviewQueueItemResponse Card { get; set; } = new();
}

public sealed class SrsBatchEnrollResponse
{
    public int CreatedCount { get; set; }
    public int LinkedCount { get; set; }
    public int ReactivatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<ReviewQueueItemResponse> Cards { get; set; } = [];
}

public sealed class SrsSuggestedCardResponse
{
    public string SuggestedKey { get; set; } = string.Empty;
    public string CardType { get; set; } = "Vocabulary";
    public string SourceType { get; set; } = "Lesson";
    public int? SourceId { get; set; }
    public int? LessonId { get; set; }
    public int? VocabularyId { get; set; }
    public int? KanjiId { get; set; }
    public int? GrammarPatternId { get; set; }
    public int? SrsCardTemplateId { get; set; }
    public string? JlptLevel { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string? Meaning { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? AudioUrl { get; set; }
    public string? SourceLabel { get; set; }
    public bool AlreadyEnrolled { get; set; }
    public int? ExistingReviewCardId { get; set; }
    public SrsCardStatus? ExistingStatus { get; set; }
}

public sealed class UserSrsPreferenceResponse
{
    public bool AutoEnrollAfterLesson { get; set; }
    public int DailyNewLimit { get; set; }
    public int DailyReviewLimit { get; set; }
    public string PreferredReviewMode { get; set; } = "flip";
    public bool AutoPlayAudio { get; set; }
    public bool ShowFuriganaHint { get; set; }
    public bool DailyReminderEnabled { get; set; }
    public int ReminderHour { get; set; }
    public int ReminderMinute { get; set; }
    public string ReminderFrequency { get; set; } = "Daily";
    public List<int> ReminderDays { get; set; } = [];
    public bool EmailReminderEnabled { get; set; }
    public bool EmailProviderConfigured { get; set; }
    public bool EmailVerified { get; set; } = true;
    public string TimezoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public string PreferredLocale { get; set; } = "vi";
    public DateTime? LastReminderSentAtUtc { get; set; }
    public DateOnly? LastReminderSentLocalDate { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class SrsReviewSessionResponse
{
    public Guid SessionKey { get; set; }
    public string StudyMode { get; set; } = "flip";
    public string SessionKind { get; set; } = "Scheduled";
    public string Status { get; set; } = "Active";
    public string? FilterCardType { get; set; }
    public string? FilterLearningState { get; set; }
    public string? SelectionScope { get; set; }
    public bool ShuffleEnabled { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public DateTime? DeadlineAtUtc { get; set; }
    public DateTime ServerUtcNow { get; set; }
    public int ElapsedSeconds { get; set; }
    public bool IsExpired { get; set; }
    public int TotalInitialCards { get; set; }
    public int ReviewedCount { get; set; }
    public int RememberedCount { get; set; }
    public int ForgotCount { get; set; }
    public int SkippedCount { get; set; }
    public int RemovedMidSessionCount { get; set; }
    public List<int> QueueCardIds { get; set; } = [];
    public List<int> CompletedCardIds { get; set; } = [];
    public List<int> RetryCardIds { get; set; } = [];
    public List<string> ActiveModesInSession { get; set; } = [];
    public List<ReviewQueueItemResponse> PendingItems { get; set; } = [];
    public List<ExcludedMcqCardReasonResponse> ExcludedMcqCards { get; set; } = [];
    public List<ExcludedMcqCardReasonResponse> ExcludedTypingCards { get; set; } = [];
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class SrsCalendarDayResponse
{
    public DateOnly Date { get; set; }
    public int ReviewedCount { get; set; }
    public int RememberedCount { get; set; }
    public int DueScheduledCount { get; set; }
    public int DueNowCount { get; set; }
    public int DueLaterCount { get; set; }
    public DateTime? EarliestDueAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public string Status { get; set; } = "None";
}
