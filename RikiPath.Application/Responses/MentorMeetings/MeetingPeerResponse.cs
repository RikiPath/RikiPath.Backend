namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MeetingPeerResponse
    {
        public string ConnectionId { get; init; } = string.Empty;
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
    }
}
