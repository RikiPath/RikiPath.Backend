namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class JoinMeetingRoomResponse
    {
        public MeetingPeerResponse Self { get; init; } = null!;
        public IReadOnlyList<MeetingPeerResponse> Participants { get; init; } = [];
    }
}
