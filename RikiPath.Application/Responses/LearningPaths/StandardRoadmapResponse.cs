namespace RikiPath.Application.Responses.LearningPaths;

public class StandardRoadmapResponse
{
    public int CurrentStudyLevelId { get; set; }
    public string CurrentStudyLevelName { get; set; } = string.Empty;
    public int? TargetJlptLevelId { get; set; }
    public string? TargetJlptLevelName { get; set; }
    public string LearningMode { get; set; } = "STANDARD";
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public int? NextLessonId { get; set; }
    public List<StandardRoadmapLessonResponse> Lessons { get; set; } = [];
}

public class StandardRoadmapLessonResponse
{
    public int LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}
