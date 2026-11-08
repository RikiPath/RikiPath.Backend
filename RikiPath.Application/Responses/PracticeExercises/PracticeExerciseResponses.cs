namespace RikiPath.Application.Responses.PracticeExercises;

public class PracticeExerciseSummaryResponse
{
    public int Id { get; set; }
    public int LessonId { get; set; }
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
    public int AttemptId { get; set; }
    public int PracticeExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public int LessonId { get; set; }
    public List<PracticeExerciseQuestionResponse> Questions { get; set; } = new();
}

public class PracticeExerciseQuestionResponse
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<PracticeExerciseOptionResponse> Options { get; set; } = new();
}

public class PracticeExerciseOptionResponse
{
    public int OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PracticeExerciseAttemptResultResponse
{
    public int AttemptId { get; set; }
    public int PracticeExerciseId { get; set; }
    public int LessonId { get; set; }
    public double ScorePercent { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public double MinimumPassingScorePercent { get; set; }
    public bool Passed { get; set; }
    public bool UnlocksNextLearningPathDay { get; set; }
}
