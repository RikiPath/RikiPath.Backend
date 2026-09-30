namespace RikiPath.Domain.Entities
{
    /// <summary>Lịch ôn SM-2 của một Kana character đối với một người học.</summary>
    public class KanaWritingPracticeCard : Base
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;
        public int KanaCharacterId { get; set; }
        public KanaCharacter KanaCharacter { get; set; } = null!;
        public double EaseFactor { get; set; } = 2.5;
        public int IntervalDays { get; set; }
        public int Repetitions { get; set; }
        public DateTime NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        public List<KanaWritingPracticeHistory>? Histories { get; set; }
    }
}
