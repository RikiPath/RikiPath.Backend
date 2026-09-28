namespace RikiPath.Domain.Entities;

/// <summary>A learner's attempt to complete a practice exercise.</summary>
public class PracticeExerciseAttempt : Base
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public int PracticeExerciseId { get; set; }
    public PracticeExercise PracticeExercise { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public double? ScorePercent { get; set; }
    public bool IsCompleted { get; set; }
    public List<LearnerPracticeAnswer>? Answers { get; set; }
}
