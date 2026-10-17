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
        public List<KanaWritingPracticeCard>? PracticeCards { get; set; }
    }
}
