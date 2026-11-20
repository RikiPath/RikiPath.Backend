namespace RikiPath.Domain.Entities;

public sealed class ContentAudioTranscription
{
    public Guid Id { get; set; }
    public string AudioUrl { get; set; } = null!;
    public string AudioUrlHash { get; set; } = null!;
    public int? CreatedByUserId { get; set; }
    public UserAccount? CreatedByUser { get; set; }
    public string Status { get; set; } = "Pending";
    public string LanguageCode { get; set; } = "ja-JP";
    public string? Transcript { get; set; }
    public string? RawTranscriptJson { get; set; }
    public double? Confidence { get; set; }
    public int AttemptCount { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
