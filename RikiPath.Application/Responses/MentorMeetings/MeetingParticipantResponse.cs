namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MeetingParticipantResponse
    {
        public int UserId { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsMentor { get; init; }
    }
}
