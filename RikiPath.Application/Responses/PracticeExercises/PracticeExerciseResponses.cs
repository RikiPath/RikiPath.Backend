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
    public List<PracticeQuestionReviewResponse> QuestionResults { get; set; } = [];
    public List<string> WeakAreas { get; set; } = [];
    public string? NextRecommendation { get; set; }
}
