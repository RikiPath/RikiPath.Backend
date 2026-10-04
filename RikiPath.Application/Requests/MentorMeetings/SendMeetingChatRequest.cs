namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class SendMeetingChatRequest
    {
        public string? Id { get; init; }
        public string? Text { get; init; }
        public string? Timestamp { get; init; }
    }
}
