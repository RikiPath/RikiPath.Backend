using RikiPath.Domain.Enums;

namespace RikiPath.Application.DTOs.ContentAuthoring;

public static class SkillTemplateKinds
{
    public const string VocabMeaning = "VocabMeaning";
    public const string VocabReading = "VocabReading";
    public const string VocabContextFill = "VocabContextFill";
    public const string VocabUsage = "VocabUsage";

    public const string KanjiReading = "KanjiReading";
    public const string KanjiMeaning = "KanjiMeaning";
    public const string KanjiContextSelect = "KanjiContextSelect";

    public const string GrammarMultipleChoice = "GrammarMultipleChoice";
    public const string GrammarFillBlank = "GrammarFillBlank";
    public const string GrammarOrdering = "GrammarOrdering";

    public const string ReadingPassageMcq = "ReadingPassageMcq";

    public const string ListeningAudioMcq = "ListeningAudioMcq";
    public const string ListeningVisualMcq = "ListeningVisualMcq";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        VocabMeaning, VocabReading, VocabContextFill, VocabUsage,
        KanjiReading, KanjiMeaning, KanjiContextSelect,
        GrammarMultipleChoice, GrammarFillBlank, GrammarOrdering,
        ReadingPassageMcq,
        ListeningAudioMcq, ListeningVisualMcq,
    };
}

public static class CanonicalQuestionTypes
{
    public const string SingleChoice = "SingleChoice";
    public const string MultipleChoice = "MultipleChoice";
    public const string FillBlank = "FillBlank";
    public const string Ordering = "Ordering";
    public const string VisualMultipleChoice = "VisualMultipleChoice";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        SingleChoice, MultipleChoice, FillBlank, Ordering, VisualMultipleChoice
    };
}

public sealed class ListeningPlaybackPolicyDto
{
    public int? MaxReplays { get; set; } = 2;
    public bool AllowPause { get; set; } = true;
    public bool AllowSeek { get; set; }
    public bool ShowTranscriptAfterSubmit { get; set; } = true;
}

public sealed class AiProvenanceDto
{
    public bool AiAssisted { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Action { get; set; }
    public string? PromptSummary { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public bool HumanReviewed { get; set; }
}

public sealed class CanonicalOptionDto
{
    public int SortOrder { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsCorrect { get; set; }
}

public sealed class CanonicalSkillMetadataDto
{
    // Vocabulary
    public string? Word { get; set; }
    public string? Reading { get; set; }
    public string? Meaning { get; set; }
    public string? PartOfSpeech { get; set; }

    // Kanji
    public string? KanjiCharacter { get; set; }
    public string? SinoVietnamese { get; set; }
    public string? OnYomi { get; set; }
    public string? KunYomi { get; set; }
    public int? StrokeCount { get; set; }

    // Grammar
    public string? GrammarPattern { get; set; }
    public string? Structure { get; set; }
    public string? UsageExplanation { get; set; }

    // Shared linguistic examples & context
    public string? ExampleSentence { get; set; }
    public string? ExampleSentenceMeaning { get; set; }
    public string? ContextSentence { get; set; }
    public string? JlptFormatCode { get; set; }

    // Structured answer fields for FillBlank / Ordering
    public List<string>? AcceptedAnswers { get; set; }
    public bool CaseSensitive { get; set; }
    public bool NormalizeKana { get; set; } = true;
    public List<string>? OrderingTokens { get; set; }
    public List<string>? OrderingAnswer { get; set; }
    public int? StarIndexZeroBased { get; set; }
}

public sealed class CanonicalPassageDto
{
    public int? Id { get; set; }
    public Guid? PassageIdentity { get; set; }
    public string PassageCode { get; set; } = string.Empty;
    public int CertificateLevelId { get; set; }
    public string CertificateLevelCode { get; set; } = string.Empty;
    public int LanguageSkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? PassageText { get; set; }
    public string? Instructions { get; set; }
    public string? ListeningSection { get; set; }
    public string? AudioUrl { get; set; }
    public int? AudioDurationSeconds { get; set; }
    public string? AudioTranscript { get; set; }
    public string? ImageUrl { get; set; }
    public ListeningPlaybackPolicyDto? PlaybackPolicy { get; set; }

    public string SourceType { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public string? RightsStatus { get; set; }
    public AiProvenanceDto? AiProvenance { get; set; }
    public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
    public int Version { get; set; } = 1;
}

public sealed class CanonicalQuestionDto
{
    public int? Id { get; set; }
    public Guid? QuestionIdentity { get; set; }
    public int Version { get; set; } = 1;
    public int? ParentQuestionId { get; set; }
    public bool IsArchivedVersion { get; set; }

    public int CertificateLevelId { get; set; }
    public string CertificateLevelCode { get; set; } = string.Empty;
    public int LanguageSkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;

    public int? PassageId { get; set; }
    public string? PassageCode { get; set; }
    public CanonicalPassageDto? Passage { get; set; }

    public string? QuestionGroupTitle { get; set; }
    public int GroupSortOrder { get; set; }
    public string TemplateKind { get; set; } = SkillTemplateKinds.VocabMeaning;
    public string QuestionType { get; set; } = CanonicalQuestionTypes.SingleChoice;
    public string QuestionText { get; set; } = string.Empty;

    public CanonicalSkillMetadataDto? SkillMetadata { get; set; }
    public List<CanonicalOptionDto> Options { get; set; } = [];
    public string? AudioUrl { get; set; }
    public int? AudioDurationSeconds { get; set; }
    public string? AudioTranscript { get; set; }
    public ListeningPlaybackPolicyDto? PlaybackPolicy { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }

    public string? Difficulty { get; set; }
    public string? Topic { get; set; }
    public List<string> Tags { get; set; } = [];

    public string SourceType { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public string? RightsStatus { get; set; }
    public AiProvenanceDto? AiProvenance { get; set; }
    public ContentStatus PublishStatus { get; set; } = ContentStatus.Draft;
    public string? ContentFingerprint { get; set; }
    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed record CanonicalValidationIssue(
    string Field,
    int? RowNumber,
    string Severity,
    string Code,
    string Message);

public sealed class CanonicalValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public bool CanSaveDraft { get; set; }
    public bool CanPublish { get; set; }
    public bool IncompleteMedia { get; set; }
    public string ContentFingerprint { get; set; } = string.Empty;
    public List<CanonicalValidationIssue> Errors { get; } = [];
    public List<CanonicalValidationIssue> Warnings { get; } = [];
}

public sealed class DuplicateMatchDto
{
    public int QuestionId { get; set; }
    public Guid QuestionIdentity { get; set; }
    public int Version { get; set; }
    public ContentStatus PublishStatus { get; set; }
    public string MatchKind { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
}

public sealed class ReviewHistoryEntryDto
{
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
    public string? Note { get; set; }
    public bool RightsConfirmed { get; set; }
}
