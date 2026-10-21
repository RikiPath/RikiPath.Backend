namespace RikiPath.Domain.Entities;

/// <summary>A persisted text message exchanged privately within one paid mentor booking.</summary>
public sealed class MentorChatMessage : Base
{
    public int Id { get; set; }
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public int SenderUserId { get; set; }
    public UserAccount Sender { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
