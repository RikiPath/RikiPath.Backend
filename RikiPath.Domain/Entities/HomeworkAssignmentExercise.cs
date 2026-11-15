namespace RikiPath.Domain.Entities;

public class HomeworkAssignmentExercise
{
    public int HomeworkAssignmentId { get; set; }
    public HomeworkAssignment HomeworkAssignment { get; set; } = null!;
    public int PracticeExerciseId { get; set; }
    public PracticeExercise PracticeExercise { get; set; } = null!;
}
