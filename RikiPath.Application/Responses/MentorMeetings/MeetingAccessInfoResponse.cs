namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MeetingAccessInfoResponse
    {
        public MeetingParticipantResponse Mentor { get; init; } = null!;
        public IReadOnlyList<MeetingParticipantResponse> Learners { get; init; } = [];
    }
}