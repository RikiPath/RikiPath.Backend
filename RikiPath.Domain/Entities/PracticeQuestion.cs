namespace RikiPath.Domain.Entities;

/// <summary>A question in a practice exercise.</summary>
public class PracticeQuestion : Base
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public int SortOrder { get; set; }
    public int PracticeExerciseId { get; set; }
    public PracticeExercise PracticeExercise { get; set; } = null!;
    public List<PracticeQuestionOption>? Options { get; set; }
    public List<LearnerPracticeAnswer>? Answers { get; set; }
}
