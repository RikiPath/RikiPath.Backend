using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

/// <summary>Server-authoritative SRS review session state enabling recovery across refresh, tab close, and multiple devices.</summary>
public class SrsReviewSession : Base
{
    public int Id { get; set; }
    public Guid SessionKey { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;

    public string StudyMode { get; set; } = "flip";
    public string SessionKind { get; set; } = "Scheduled"; // Scheduled | Practice
    public string Status { get; set; } = "Active"; // Active | Completed | Abandoned

    public string? FilterCardType { get; set; }
    public string? FilterJlptLevel { get; set; }
    public string? FilterSourceType { get; set; }
    public string? FilterLearningState { get; set; }
    public string? SelectionScope { get; set; }

    public bool ShuffleEnabled { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public DateTime? DeadlineAtUtc { get; set; }

    public int RequestedCount { get; set; }
    public int TotalInitialCards { get; set; }

    public string QueueCardIdsJson { get; set; } = "[]";
    public string CompletedCardIdsJson { get; set; } = "[]";
    public string RetryCardIdsJson { get; set; } = "[]";
    public string ProcessedStepKeysJson { get; set; } = "[]";

    public int ReviewedCount { get; set; }
    public int RememberedCount { get; set; }
    public int ForgotCount { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
