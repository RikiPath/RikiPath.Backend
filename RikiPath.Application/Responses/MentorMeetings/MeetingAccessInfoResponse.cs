namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MeetingAccessInfoResponse
    {
        public MeetingParticipantResponse Learner { get; init; } = null!;
        public MeetingParticipantResponse Mentor { get; init; } = null!;
    }
}
