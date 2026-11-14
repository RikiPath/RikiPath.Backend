namespace RikiPath.Domain.Entities;

public class HomeworkAssignmentLesson
{
    public int HomeworkAssignmentId { get; set; }
    public HomeworkAssignment HomeworkAssignment { get; set; } = null!;
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
}
