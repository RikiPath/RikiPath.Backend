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
    public int? PlacementAttemptId { get; set; }
    public string? ComparisonLevelCode { get; set; }
    public List<StandardRoadmapSkillResponse> Skills { get; set; } = [];
}

public class StandardRoadmapSkillResponse
{
    public LearningPathSectionAssessmentResponse Assessment { get; set; } = new();
    public List<string> UnavailableLevelCodes { get; set; } = [];
}

public class StandardRoadmapLessonResponse
{
    public int LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsStudied { get; set; }
    public string? SkillCode { get; set; }
    public string? LanguageSkillName { get; set; }
    public string? LevelCode { get; set; }
    public bool HasPracticeExercise { get; set; }
    public bool PracticePassed { get; set; }
    public bool IsLocked { get; set; }
    public int KanjiCount { get; set; }
    public int VocabularyCount { get; set; }
    public int GrammarCount { get; set; }
}
