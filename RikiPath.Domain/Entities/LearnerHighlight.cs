namespace RikiPath.Domain.Entities;

/// <summary>Learner text highlight saved on lesson, grammar, reading, or note content.</summary>
public class LearnerHighlight : Base
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;

    public string TargetType { get; set; } = "Lesson";
    public int? TargetId { get; set; }
    public string? TargetKey { get; set; }
    public string? SourcePath { get; set; }
    public string? SourceTitle { get; set; }

    public string SelectedText { get; set; } = string.Empty;
    public string Color { get; set; } = "yellow";

    public string? AnchorPrefix { get; set; }
    public string? AnchorSuffix { get; set; }
    public int? StartOffset { get; set; }
    public int? EndOffset { get; set; }
    public string? ContentVersionHash { get; set; }

    public string? Note { get; set; }
    public string? TranslationSnapshot { get; set; }

    public int? LinkedReviewCardId { get; set; }
    public ReviewCard? LinkedReviewCard { get; set; }

    public int? LinkedNoteEntryId { get; set; }
    public LearnerNoteEntry? LinkedNoteEntry { get; set; }
}
