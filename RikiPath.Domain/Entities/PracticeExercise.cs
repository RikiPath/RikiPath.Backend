using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

/// <summary>A skill-focused exercise attached to a lesson.</summary>
public class PracticeExercise : Base
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public int LanguageSkillId { get; set; }
    public LanguageSkill LanguageSkill { get; set; } = null!;
    public int ContentAuthorId { get; set; }
    public UserAccount ContentAuthor { get; set; } = null!;
    public int SortOrder { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public bool IsApproved { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewNote { get; set; }
    public List<PracticeQuestion>? Questions { get; set; }
    public List<PracticeExerciseAttempt>? Attempts { get; set; }
}
