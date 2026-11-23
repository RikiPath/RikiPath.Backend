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
        /// <summary>Published Vietnamese translation of <see cref="ExampleSentence"/> (null when the author did not provide one).</summary>
        public string? ExampleSentenceMeaning { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
    }

    public class QuestionBankOptionResponse
    {
        public int Id { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsCorrect { get; set; }
        public int SortOrder { get; set; }
    }

    public class QuestionBankCardResponse
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string JlptLevelName { get; set; } = string.Empty;
        public int? CertificateLevelId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public int? LanguageSkillId { get; set; }
        public string MockTestTitle { get; set; } = string.Empty;
        public int MockTestId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string? AnswerSummary { get; set; }
        public int? QuestionBankItemId { get; set; }
        public Guid? QuestionIdentity { get; set; }
        public int Version { get; set; } = 1;
        public string PublishStatus { get; set; } = "Published";
        public string? TemplateKind { get; set; }
        public int? PassageId { get; set; }
        public string? PassageTitle { get; set; }
        public string? SourceType { get; set; }
        public IReadOnlyList<QuestionBankOptionResponse> Options { get; set; } = [];
    }
}
