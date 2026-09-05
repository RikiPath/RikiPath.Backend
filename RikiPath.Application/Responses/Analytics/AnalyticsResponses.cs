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

    public class SkillBreakdownItem
    {
        public string SkillName { get; set; } = string.Empty;
        public double AverageScore { get; set; } // 0-100
        public int AttemptCount { get; set; }
    }
}
