using Domain.Enums;
using RikiPath.Domain.Entities;

namespace Domain.Entities
{
    public class KanjiEntry : Base, IReviewableContent
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
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }

        public int JlptLevelId { get; set; }
        public JlptLevel JlptLevel { get; set; }
        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; }
        public int? ReviewedById { get; set; }
        public UserAccount? ReviewedBy { get; set; }

        public List<LessonKanji>? LessonKanjis { get; set; }
        public List<VocabularyNoteEntry>? VocabularyNoteEntries { get; set; }
        public List<ReviewItem>? ReviewItems { get; set; }
    }
}
