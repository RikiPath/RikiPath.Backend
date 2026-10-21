namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class MentorChatMessageResponse
{
    public int Id { get; init; }
    public int BookingId { get; init; }
    public int SenderUserId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public DateTime? ReadAt { get; init; }
}
