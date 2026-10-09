namespace RikiPath.Domain.Entities
{
    public class ContentLevelMapping : Base
    {
        public int Id { get; set; }

        // Khóa ngoại trỏ về CertificateLevel
        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; }

        public int? KanjiId { get; set; }
        public Kanji? Kanji { get; set; }

        public int? VocabularyId { get; set; }
        public Vocabulary? Vocabulary { get; set; }

        public int? GrammarPatternId { get; set; }
        public GrammarPattern? GrammarPattern { get; set; }
    }
}
