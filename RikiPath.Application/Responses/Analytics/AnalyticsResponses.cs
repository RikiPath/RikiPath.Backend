namespace RikiPath.Application.Responses.Analytics
{
    public class StudyStreakResponse
    {
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public bool IsActiveToday { get; set; }
    }

    /// <summary>
    /// Streak celebration state derived by the server (Asia/Ho_Chi_Minh study day). A celebration event exists only
    /// when today already has a valid learning activity; it is pending until claimed once (any tab/device).
    /// </summary>
    public class StreakCelebrationResponse
    {
        /// <summary>True when today's streak event exists and has not been claimed on any device yet.</summary>
        public bool HasPendingEvent { get; set; }

        /// <summary>Stable event identity: <c>streak:{yyyyMMdd}:{days}</c>. Null when no event exists today.</summary>
        public string? EventId { get; set; }

        /// <summary>
        /// <c>daily</c> / <c>milestone</c> (today already studied — celebrate) or <c>atRisk</c> (live streak, nothing
        /// studied yet today — keep-the-streak reminder). Each kind is claimable once per study day.
        /// </summary>
        public string? EventType { get; set; }

        public DateTime? StudyDate { get; set; }

        /// <summary>UTC instant when today's Asia/Ho_Chi_Minh study day ends (keep-streak countdown).</summary>
        public DateTime StudyDayEndsAtUtc { get; set; }

        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public bool IsActiveToday { get; set; }

        /// <summary>Milestone reached by this event, when <see cref="EventType"/> is <c>milestone</c>.</summary>
        public int? Milestone { get; set; }
        public int? NextMilestone { get; set; }
        public List<int> Milestones { get; set; } = [];

        /// <summary>Study days (Asia/Ho_Chi_Minh) with verified activity in the current Monday–Sunday week.</summary>
        public List<DateTime> WeekActivityDates { get; set; } = [];

        /// <summary>Set by the claim endpoint: true only for the single request that won the claim.</summary>
        public bool Claimed { get; set; }
    }

    public class CompletionStatsResponse
    {
        public bool HasValidRoadmap { get; set; }
        public int TotalLessons { get; set; }
        public int CompletedLessons { get; set; }
        public double CompletionPercent { get; set; }
    }

    public class LanguageSkillBreakdownItem
    {
        public string LanguageSkillName { get; set; } = string.Empty;
        public double AverageScore { get; set; } // 0-100
        public int AttemptCount { get; set; }
    }

    public sealed class SkillMonthlyAnalyticsResponse
    {
        public int MonthsRequested { get; set; }
        public string? AssessedJlptLevel { get; set; }
        public string? TargetJlptLevel { get; set; }
        public int TotalAttempts { get; set; }
        public int MockTestAttempts { get; set; }
        public int PlacementAttempts { get; set; }
        public double? CurrentOverallScorePercent { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<SkillMonthlyAnalyticsItem> Skills { get; set; } = [];
    }

    public sealed class SkillMonthlyAnalyticsItem
    {
        public string LanguageSkillName { get; set; } = string.Empty;
        public double? CurrentScorePercent { get; set; }
        public string CurrentAssessment { get; set; } = string.Empty;
        public double? ChangeFromPreviousRecordedMonth { get; set; }
        public string? ComparedMonth { get; set; }
        public int TotalAttempts { get; set; }
        public int MockTestAttempts { get; set; }
        public int PlacementAttempts { get; set; }
        public List<SkillMonthlyScore> MonthlyScores { get; set; } = [];
    }

    public sealed class SkillMonthlyScore
    {
        public string Month { get; set; } = string.Empty;
        public double? ScorePercent { get; set; }
        public int AttemptCount { get; set; }
        public int MockTestAttemptCount { get; set; }
        public int PlacementAttemptCount { get; set; }
        public int QuestionCount { get; set; }
    }
}
