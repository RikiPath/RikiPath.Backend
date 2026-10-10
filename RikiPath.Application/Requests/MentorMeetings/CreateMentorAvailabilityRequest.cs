namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class CreateMentorAvailabilityRequest
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

}
