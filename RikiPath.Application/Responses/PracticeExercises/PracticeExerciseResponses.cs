namespace RikiPath.Application.Responses.PracticeExercises;

public class PracticeExerciseSummaryResponse
{
    public string? ImageUrl { get; set; }
    public int Id { get; set; }
    public int PracticeExerciseId => Id;
    public double MinimumPassingScorePercent { get; set; } = 70;
    public bool Passed { get; set; }
    public int LessonId { get; set; }
    public int? TopicId { get; set; }
    public int? CertificateLevelId { get; set; }
    public string Scope { get; set; } = "LessonQuickCheck";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LanguageSkillId { get; set; }
    public string LanguageSkillName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int QuestionCount { get; set; }
    public double? BestScorePercent { get; set; }
    public PracticeLatestAttemptResponse? LatestAttempt { get; set; }
}

public class PracticeExerciseAttemptResponse
{
    public string? ImageUrl { get; set; }
    public int AttemptId { get; set; }
    public int PracticeExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string Scope { get; set; } = "LessonQuickCheck";
    public int LessonId { get; set; }
    public int? TopicId { get; set; }
    public int? CertificateLevelId { get; set; }
    public double PassingScorePercent { get; set; } = 70;
    public List<PracticeExerciseQuestionResponse> Questions { get; set; } = new();
}

public class PracticeExerciseQuestionResponse
{
    public string? ImageUrl { get; set; }
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<PracticeExerciseOptionResponse> Options { get; set; } = new();
}

public class PracticeExerciseOptionResponse
{
    public string? ImageUrl { get; set; }
    public int OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PracticeQuestionReviewResponse
{
    public int QuestionId { get; set; }
    public int SortOrder { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int? SelectedOptionId { get; set; }
    public string? SelectedOptionText { get; set; }
    public int? CorrectOptionId { get; set; }
    public string? CorrectOptionText { get; set; }
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
}

public class PracticeExerciseAttemptResultResponse
{
    public int AttemptId { get; set; }
    public int PracticeExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string Scope { get; set; } = "LessonQuickCheck";
    public int LessonId { get; set; }
    public double ScorePercent { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public double MinimumPassingScorePercent { get; set; }
    public bool Passed { get; set; }
    public bool UnlocksNextLearningPathDay { get; set; }

    /// <summary>
    /// Roadmap progression after this submission (Standard roadmap Quick Checks and checkpoints only), read back
    /// from the roadmap the server now serves. Null when not applicable or when it could not be computed.
    /// </summary>
    public RoadmapProgressionResponse? Progression { get; set; }
    public List<PracticeQuestionReviewResponse> QuestionResults { get; set; } = [];
    public List<string> WeakAreas { get; set; } = [];
    public string? NextRecommendation { get; set; }
}

/// <summary>
/// What a roadmap submission changed. <see cref="Milestone"/> is only set on a real transition (first pass):
/// LESSON_COMPLETED, STAGE_CHECKPOINT_UNLOCKED (last lesson done → stage check opens), STAGE_COMPLETED,
/// ROADMAP_COMPLETED. A retake of something already passed reports NONE; a failed attempt reports NOT_PASSED.
/// </summary>
public class RoadmapProgressionResponse
{
    public string Milestone { get; set; } = "NONE";
    public bool WasAlreadyPassed { get; set; }
    public int? LessonId { get; set; }
    public string? LessonTitle { get; set; }
    public int? StageId { get; set; }
    public string? StageTitle { get; set; }
    public int? StageNumber { get; set; }
    public int StageCompletedLessons { get; set; }
    public int StageTotalLessons { get; set; }
    public string? StageStatus { get; set; }
    public int? NextLessonId { get; set; }
    public string? NextLessonTitle { get; set; }
    public int? CheckpointExerciseId { get; set; }
    public string? CheckpointTitle { get; set; }
    public string? CheckpointScope { get; set; }
    public bool CheckpointUnlocked { get; set; }
    public int? NextStageId { get; set; }
    public string? NextStageTitle { get; set; }
    public int? NextStageNumber { get; set; }
    public int RoadmapCompletedLessons { get; set; }
    public int RoadmapTotalLessons { get; set; }
    public int RoadmapCompletedStages { get; set; }
    public int RoadmapTotalStages { get; set; }
    public string? TargetLevelCode { get; set; }

    /// <summary>Learning Moment occurrence key the client claims when it shows this celebration (no dashboard replay).</summary>
    public string? MomentOccurrenceKey { get; set; }
}
