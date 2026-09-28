namespace RikiPath.Domain.Entities;

/// <summary>A learner's submission for an assignment.</summary>
public class HomeworkSubmission : Base
{
    public int Id { get; set; }
    public int HomeworkAssignmentId { get; set; }
    public HomeworkAssignment HomeworkAssignment { get; set; } = null!;
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string? TextContent { get; set; }
    public string? FileUrl { get; set; }
    public DateTime SubmittedAt { get; set; }
    public GradingResult? GradingResult { get; set; }
}
