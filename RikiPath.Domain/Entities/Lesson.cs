using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class Lesson : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string VideoUrl { get; set; }
        public int DurationSeconds { get; set; }
        public int SortOrder { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }
        /// <summary>Tên Admin đã duyệt bài — hệ thống chỉ có 1 Admin nên không cần FK riêng.</summary>
        public string? ReviewedByName { get; set; }

        public int CertificationLevelId { get; set; }
        public CertificationLevel CertificationLevel { get; set; }
        public int SkillId { get; set; }
        public Skill Skill { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }

        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<LessonGrammar>? LessonGrammars { get; set; }
    }
}