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

    // Adaptive Roadmap V2 additive fields
    public bool HasPlacementEvidence { get; set; }
    public string PlacementDecision { get; set; } = "NOT_TAKEN";
    public int DailyStudyMinutes { get; set; } = 30;
    public int SrsDueTodayCount { get; set; }
    public int DaysSinceLastStudy { get; set; }
    public string? WorkloadAdvice { get; set; }
    public string? AdaptiveNotice { get; set; }
    public string? NextRecommendationReason { get; set; }
    public string? NextActionType { get; set; }
    public int? NextCheckpointExerciseId { get; set; }
    public List<AdaptiveSkillProfileItemResponse> SkillProfile { get; set; } = [];
    public List<StandardRoadmapStageResponse> Stages { get; set; } = [];
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

    // Adaptive Roadmap V2 additive fields
    public int? TopicId { get; set; }
    public string? TopicTitle { get; set; }
    public string? Description { get; set; }
    public int EstimatedMinutes { get; set; } = 15;
    public int? PrerequisiteLessonId { get; set; }
    public string? LearningObjectives { get; set; }
    public string CompletionState { get; set; } = "NotStarted";
    public bool IsRecommendedNext { get; set; }
    public bool IsRemediation { get; set; }
    public string? RecommendationReason { get; set; }
    public double? BestPracticeScore { get; set; }
    public int PracticeAttemptCount { get; set; }
    public int? QuickCheckExerciseId { get; set; }
    public int QuickCheckQuestionCount { get; set; }
}
