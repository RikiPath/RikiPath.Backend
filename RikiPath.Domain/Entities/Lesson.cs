using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class Lesson : Base
    {
        public string? ImageUrl { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? AudioUrl { get; set; }
        public int SortOrder { get; set; }
        public int EstimatedMinutes { get; set; } = 15;
        public int? TopicId { get; set; }
        public Topic? Topic { get; set; }
        public int? PrerequisiteLessonId { get; set; }
        public Lesson? PrerequisiteLesson { get; set; }
        public string? LearningObjectives { get; set; }
        public string? SourceProvenance { get; set; }
        public List<HomeworkAssignmentLesson> HomeworkAssignmentLessons { get; set; } = new();

        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Published;
        public int Version { get; set; } = 1;
        public DateTime? UnpublishedAtUtc { get; set; }
        public string? UnpublishedReason { get; set; }

        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<LessonGrammar>? LessonGrammars { get; set; }
    }
}
