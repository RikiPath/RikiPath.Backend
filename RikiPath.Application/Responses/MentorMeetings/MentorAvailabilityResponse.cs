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
        public bool IsApproved { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }
        public int BookedCount { get; set; }
        public int MaxLearners { get; set; }
        public string? AdminName { get; set; }
    }
}
