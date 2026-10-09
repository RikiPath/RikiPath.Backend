using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class Kanji : Base
    {
        public int Id { get; set; }
        public string Character { get; set; }
        public string Meaning { get; set; }
        public string? SinoVietnamese { get; set; }
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }
        public int StrokeCount { get; set; }
        public string? StrokeOrderImageUrl { get; set; }
        public string? AudioUrl { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public bool IsApproved { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewedByName { get; set; }


        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }

        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<LearnerNoteEntry>? LearnerNoteEntries { get; set; }
        public List<ReviewCard>? ReviewCards { get; set; }
        public List<ContentLevelMapping>? ContentLevelMappings { get; set; }
    }
}