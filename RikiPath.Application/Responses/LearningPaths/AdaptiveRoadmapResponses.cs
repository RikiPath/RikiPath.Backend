namespace RikiPath.Application.Responses.LearningPaths;

public class AdaptiveSkillProfileItemResponse
{
    public string SkillCode { get; set; } = string.Empty;
    public string SkillLabel { get; set; } = string.Empty;
    public string ProficiencyStatus { get; set; } = "NOT_YET_ASSESSED";
    public double? ProficiencyPercent { get; set; }
    public string Confidence { get; set; } = "low";
    public string Priority { get; set; } = "needs_assessment";
    public int EvidenceCount { get; set; }
    public double WeightedQuestionCount { get; set; }
    public DateTime? LastAssessedAtUtc { get; set; }
    public List<string> EvidenceSources { get; set; } = [];
    public List<string> WeakTopics { get; set; } = [];
    public List<string> StrongTopics { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public class RoadmapCheckpointResponse
{
    public int ExerciseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string SkillCode { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public double PassingScorePercent { get; set; } = 70;
    public bool IsUnlocked { get; set; }
    public bool IsPassed { get; set; }
    public double? BestScorePercent { get; set; }
    public int AttemptCount { get; set; }
    public string Status { get; set; } = "Locked";
    public string? LockReason { get; set; }
}

public class StandardRoadmapStageResponse
{
    public int StageId { get; set; }
    public string? StageCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LearningObjectives { get; set; }
    public string LevelCode { get; set; } = string.Empty;
    public bool IsBridgeStage { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public int EstimatedMinutesTotal { get; set; }
    public List<StandardRoadmapLessonResponse> Lessons { get; set; } = [];
    public RoadmapCheckpointResponse? GroupQuiz { get; set; }
    public RoadmapCheckpointResponse? StageAssessment { get; set; }
}
