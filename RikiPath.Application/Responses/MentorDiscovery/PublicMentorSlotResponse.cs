namespace RikiPath.Application.Responses.MentorDiscovery;

public sealed class PublicMentorSlotResponse
{
    public int SlotId { get; set; }
    public int MentorId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalStart { get; set; }
    public TimeOnly LocalEnd { get; set; }
    public int DurationMinutes { get; set; }
    public string? DaypartCode { get; set; }
    public string Status { get; set; } = "Available";
    public bool IsAvailable { get; set; } = true;
}
