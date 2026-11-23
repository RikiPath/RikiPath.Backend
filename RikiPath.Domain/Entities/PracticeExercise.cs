using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

/// <summary>A skill-focused exercise or checkpoint attached to a lesson, chapter/topic, or level stage.</summary>
public class PracticeExercise : Base
{
    public string? ImageUrl { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = PracticeExerciseScopes.LessonQuickCheck;
    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public int? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public int? CertificateLevelId { get; set; }
    public CertificateLevel? CertificateLevel { get; set; }
    public int LanguageSkillId { get; set; }
    public LanguageSkill LanguageSkill { get; set; } = null!;
    public int SortOrder { get; set; }
    public double PassingScorePercent { get; set; } = 70;
    public List<PracticeQuestion>? Questions { get; set; }
    public List<PracticeExerciseAttempt>? Attempts { get; set; }
}
