namespace RikiPath.Domain.Entities;

/// <summary>An assignment from a mentor to a learner, optionally associated with a lesson.</summary>
public class HomeworkAssignment : Base
{
    public int Id { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = "Assigned";
    public HomeworkSubmission? Submission { get; set; }
}
