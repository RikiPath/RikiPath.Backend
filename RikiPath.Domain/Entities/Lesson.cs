using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class Lesson : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }
        public int LanguageSkillId { get; set; }
        public LanguageSkill LanguageSkill { get; set; }

        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<LessonVocabulary>? LessonVocabularies { get; set; }
        public List<LessonGrammar>? LessonGrammars { get; set; }
    }
}
