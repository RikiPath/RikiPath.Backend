using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>Ký tự Hiragana hoặc Katakana có thể được dùng trong bài luyện viết.</summary>
    public class KanaCharacter : Base
    {
        public int Id { get; set; }
        public string Character { get; set; } = string.Empty;
        public KanaCharacterType Type { get; set; }
        public string Romaji { get; set; } = string.Empty;
        public int StrokeCount { get; set; }
        public string? StrokeOrderImageUrl { get; set; }
        public string? AudioUrl { get; set; }

        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public bool IsApproved { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewedByName { get; set; }

        public int ContentAuthorId { get; set; }
        public UserAccount ContentAuthor { get; set; } = null!;
        public List<KanaWritingPracticeCard>? PracticeCards { get; set; }
    }
}
