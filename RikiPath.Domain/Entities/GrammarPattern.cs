using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class GrammarPattern : Base
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Structure { get; set; }
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;

        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }

        public List<LessonGrammar>? LessonGrammars { get; set; }
        public List<ReviewCard>? ReviewCards { get; set; }
        public List<ContentLevelMapping>? ContentLevelMappings { get; set; }
    }
}
