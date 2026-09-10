using System.Collections.Generic;

using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// A single video lesson that belongs to exactly one <see cref="Course"/> and is
    /// tagged with the <see cref="Skill"/> it develops. Video files live in AWS S3;
    /// VideoUrl stores the CloudFront-delivered URL. JLPT level, category and content
    /// authorship are inherited from the parent Course.
    /// </summary>
    public class Lesson : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string VideoUrl { get; set; }
        public int DurationSeconds { get; set; }
        public int SortOrder { get; set; }
        public ContentStatus Status { get; set; } = ContentStatus.Draft;

        public int CourseId { get; set; }
        public Course Course { get; set; }
        public int SkillId { get; set; }
        public Skill Skill { get; set; }

        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<LessonGrammar>? LessonGrammars { get; set; }
    }
}
