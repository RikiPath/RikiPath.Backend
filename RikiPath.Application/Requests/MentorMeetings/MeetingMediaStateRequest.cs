namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class MeetingMediaStateRequest
    {
        public bool IsMicOn { get; init; } = true;
        public bool IsCamOn { get; init; } = true;
        public bool IsScreenSharing { get; init; }
        public string? ScreenStreamId { get; init; }
    }
}
