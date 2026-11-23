using System.ComponentModel.DataAnnotations;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.DTOs.SrsTemplates;

public sealed class SaveSrsCardTemplateRequest
{
    [Required]
    [MaxLength(30)]
    public string CardType { get; set; } = "Vocabulary"; // Vocabulary | Kanji | Grammar | Custom

    [MaxLength(500)]
    public string? FrontText { get; set; }

    [MaxLength(1000)]
    public string? BackText { get; set; }

    [MaxLength(300)]
    public string? Reading { get; set; }

    [MaxLength(500)]
    public string? Meaning { get; set; }

    [MaxLength(1000)]
    public string? ExampleSentence { get; set; }

    [MaxLength(1000)]
    public string? ExampleMeaning { get; set; }

    [MaxLength(2000)]
    public string? UsageNotes { get; set; }

    public List<string>? AcceptedReadings { get; set; }
    public List<string>? Synonyms { get; set; }

    [MaxLength(1000)]
    public string? AudioUrl { get; set; }

    [MaxLength(10)]
    public string? JlptLevel { get; set; } // N5 | N4 | N3 | N2

    public int? LessonId { get; set; }
    public int? VocabularyId { get; set; }
    public int? KanjiId { get; set; }
    public int? GrammarPatternId { get; set; }
}

public sealed class ReviewSrsCardTemplateRequest
{
    public bool Approve { get; set; }

    [MaxLength(1000)]
    public string? ReviewNote { get; set; }
}

public sealed class TogglePublishContentRequest
{
    [Required]
    [MaxLength(40)]
    public string EntityType { get; set; } = "SrsCardTemplate"; // Lesson | Vocabulary | Kanji | GrammarPattern | SrsCardTemplate

    [Required]
    public int EntityId { get; set; }

    public bool Publish { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class SrsCardTemplateResponse
{
    public int Id { get; set; }
    public string CardType { get; set; } = "Vocabulary";
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string? Reading { get; set; }
    public string? Meaning { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleMeaning { get; set; }
    public string? UsageNotes { get; set; }
    public List<string> AcceptedReadings { get; set; } = [];
    public List<string> Synonyms { get; set; } = [];
    public string? AudioUrl { get; set; }
    public string? JlptLevel { get; set; }
    public int? CertificateLevelId { get; set; }
    public int? LessonId { get; set; }
    public int? VocabularyId { get; set; }
    public int? KanjiId { get; set; }
    public int? GrammarPatternId { get; set; }
    public ContentStatus Status { get; set; }
    public int Version { get; set; }
    public int? SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? ReviewedByAdminName { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? UnpublishedAtUtc { get; set; }
    public string? UnpublishedReason { get; set; }
    public bool AlreadyEnrolled { get; set; }
    public int? ExistingReviewCardId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public sealed class AdminPublishCatalogItemResponse
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? JlptLevel { get; set; }
    public ContentStatus Status { get; set; }
    public int Version { get; set; }
    public DateTime? UnpublishedAtUtc { get; set; }
    public string? UnpublishedReason { get; set; }
    public int EnrolledLearnerCount { get; set; }
}
