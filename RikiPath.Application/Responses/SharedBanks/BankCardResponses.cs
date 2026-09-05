namespace RikiPath.Application.Responses.SharedBanks
{
    public class VocabularyCardResponse
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string PartOfSpeech { get; set; } = string.Empty;
        public string ExampleSentence { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
    }

    public class KanjiCardResponse
    {
        public int Id { get; set; }
        public string Character { get; set; } = string.Empty;
        public string OnYomi { get; set; } = string.Empty;
        public string? KunYomi { get; set; }
        public string? SinoVietnamese { get; set; }
        public string Meaning { get; set; } = string.Empty;
        public int StrokeCount { get; set; }
        public string? StrokeOrderUrl { get; set; }
        public string? AudioUrl { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
    }

    public class GrammarCardResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string ExampleSentence { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
    }
}
