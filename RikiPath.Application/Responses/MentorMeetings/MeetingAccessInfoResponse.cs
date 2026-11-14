namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MeetingAccessInfoResponse
    {
        public int Capacity { get; init; } = 5;
        public MeetingParticipantResponse Mentor { get; init; } = null!;
        public IReadOnlyList<MeetingParticipantResponse> Learners { get; init; } = [];
    }
}