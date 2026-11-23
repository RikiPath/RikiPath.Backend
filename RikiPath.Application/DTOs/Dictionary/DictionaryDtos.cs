using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.DTOs.Dictionary;

public sealed class TranslateSelectionRequest
{
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ContextSentence { get; set; }

    [MaxLength(10)]
    public string TargetLanguage { get; set; } = "vi";
}

public sealed class DictionaryLookupItemResponse
{
    public string EntryType { get; set; } = "Vocabulary"; // Vocabulary | Kanji | Grammar
    public int EntryId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string Meaning { get; set; } = string.Empty;
    public string? SinoVietnamese { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? JlptLevel { get; set; }
    public string? AudioUrl { get; set; }
    public bool AlreadyEnrolledInSrs { get; set; }
    public int? ExistingReviewCardId { get; set; }
}

public sealed class DictionaryLookupResponse
{
    public string Query { get; set; } = string.Empty;
    public List<DictionaryLookupItemResponse> Items { get; set; } = [];
}

public sealed class TranslateBreakdownItem
{
    public string Surface { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string Meaning { get; set; } = string.Empty;
}

public sealed class TranslateSelectionResponse
{
    public string Query { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string? GrammarNote { get; set; }
    public string Source { get; set; } = "ApprovedBank"; // ApprovedBank | AiTranslation
    public bool FromCache { get; set; }
    public string? SuggestedCardType { get; set; }
    public int? BankEntryId { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public List<TranslateBreakdownItem> Breakdown { get; set; } = [];
}
