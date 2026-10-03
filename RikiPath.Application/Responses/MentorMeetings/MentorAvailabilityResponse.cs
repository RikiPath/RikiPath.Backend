namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorAvailabilityResponse
    {
        public int Id { get; set; }
        public int MentorId { get; set; }
        public string MentorName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }
    }
}
