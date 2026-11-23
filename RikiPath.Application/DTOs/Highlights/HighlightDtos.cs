using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.DTOs.Highlights;

public sealed class CreateHighlightRequest
{
    [Required]
    [MaxLength(40)]
    public string TargetType { get; set; } = "Lesson"; // Lesson | Grammar | Vocabulary | Kanji | Reading | Note | Custom

    public int? TargetId { get; set; }

    [MaxLength(200)]
    public string? TargetKey { get; set; }

    [MaxLength(300)]
    public string? SourcePath { get; set; }

    [MaxLength(200)]
    public string? SourceTitle { get; set; }

    [Required]
    [MaxLength(1000)]
    public string SelectedText { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Color { get; set; } = "yellow"; // yellow | mint | sakura

    [MaxLength(200)]
    public string? AnchorPrefix { get; set; }

    [MaxLength(200)]
    public string? AnchorSuffix { get; set; }

    public int? StartOffset { get; set; }
    public int? EndOffset { get; set; }

    [MaxLength(128)]
    public string? ContentVersionHash { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? TranslationSnapshot { get; set; }

    public int? LinkedReviewCardId { get; set; }
    public int? LinkedNoteEntryId { get; set; }
}

public sealed class UpdateHighlightRequest
{
    [MaxLength(20)]
    public string? Color { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    [MaxLength(1000)]
    public string? TranslationSnapshot { get; set; }

    public int? LinkedReviewCardId { get; set; }
    public int? LinkedNoteEntryId { get; set; }
}

public sealed class HighlightToSrsRequest
{
    [MaxLength(500)]
    public string? FrontText { get; set; }

    [MaxLength(1000)]
    public string? BackText { get; set; }

    [MaxLength(300)]
    public string? Reading { get; set; }

    [MaxLength(500)]
    public string? Meaning { get; set; }

    [MaxLength(10)]
    public string? JlptLevel { get; set; }

    [MaxLength(1000)]
    public string? PersonalNote { get; set; }
}

public sealed class LearnerHighlightResponse
{
    public int Id { get; set; }
    public string TargetType { get; set; } = "Lesson";
    public int? TargetId { get; set; }
    public string? TargetKey { get; set; }
    public string? SourcePath { get; set; }
    public string? SourceTitle { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public string Color { get; set; } = "yellow";
    public string ColorHex { get; set; } = "#F6D56D";
    public string? AnchorPrefix { get; set; }
    public string? AnchorSuffix { get; set; }
    public int? StartOffset { get; set; }
    public int? EndOffset { get; set; }
    public string? ContentVersionHash { get; set; }
    public string? Note { get; set; }
    public string? TranslationSnapshot { get; set; }
    public int? LinkedReviewCardId { get; set; }
    public int? LinkedNoteEntryId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
