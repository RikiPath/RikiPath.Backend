namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class MentorChatThreadResponse
{
    public int BookingId { get; init; }
    public int LearnerId { get; init; }
    public string LearnerName { get; init; } = string.Empty;
    public int MentorId { get; init; }
    public string MentorName { get; init; } = string.Empty;
    public DateTime? ScheduledAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public string BookingStatus { get; init; } = string.Empty;
    public bool CanSendMessages { get; init; }
    public int UnreadCount { get; init; }
    public MentorChatMessageResponse? LastMessage { get; init; }
}
