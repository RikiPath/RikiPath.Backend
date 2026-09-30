namespace RikiPath.Domain.Entities;

/// <summary>A learner's response to one question in an exercise attempt.</summary>
public class LearnerPracticeAnswer : Base
{
    public int Id { get; set; }
    public int PracticeExerciseAttemptId { get; set; }
    public PracticeExerciseAttempt PracticeExerciseAttempt { get; set; } = null!;
    public int PracticeQuestionId { get; set; }
    public PracticeQuestion PracticeQuestion { get; set; } = null!;
    public int? SelectedOptionId { get; set; }
    public PracticeQuestionOption? SelectedOption { get; set; }
    public string? TextAnswer { get; set; }
    public bool? IsCorrect { get; set; }
}
