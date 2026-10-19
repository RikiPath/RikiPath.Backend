namespace RikiPath.Application.Responses.Lessons
{
    public sealed class LearningDiscoveryResponse
    {
        public int CertificateLevelId { get; init; }
        public string LevelCode { get; init; } = string.Empty;
        public int TotalLessons { get; init; }
        public int CompletedLessons { get; init; }
        public int OverallProgressPercent { get; init; }
        public List<LearningLessonSummaryResponse> Lessons { get; init; } = [];
    }

    public sealed class LearningLessonSummaryResponse
    {
        public int LessonId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int SortOrder { get; init; }
        public int LanguageSkillId { get; init; }
        public string LanguageSkillName { get; init; } = string.Empty;
        public int ProgressPercent { get; init; }
        public int ResumePositionSeconds { get; init; }
        public bool IsCompleted { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}
