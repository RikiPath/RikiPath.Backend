using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class GrammarPattern : Base
    {
        public string? ImageUrl { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Structure { get; set; }
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Published;
        public int Version { get; set; } = 1;
        public DateTime? UnpublishedAtUtc { get; set; }
        public string? UnpublishedReason { get; set; }

        public List<LessonGrammar>? LessonGrammars { get; set; }
        public List<ReviewCard>? ReviewCards { get; set; }
        public List<ContentLevelMapping>? ContentLevelMappings { get; set; }
    }
}
