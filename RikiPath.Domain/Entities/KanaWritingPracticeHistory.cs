using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>Kết quả của một lượt luyện viết Kana.</summary>
    public class KanaWritingPracticeHistory : Base
    {
        public int Id { get; set; }
        public int KanaWritingPracticeCardId { get; set; }
        public KanaWritingPracticeCard Card { get; set; } = null!;
        public int TotalMistakes { get; set; }
        public int Quality { get; set; }
        public ReviewRating Rating { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}
