namespace RikiPath.Application.Responses.Analytics
{
    public class StudyStreakResponse
    {
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public bool IsActiveToday { get; set; }
    }

    public class CompletionStatsResponse
    {
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
