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
        public int AdvanceDaysAtCreation { get; set; }
        public bool MeetsAdvanceRule { get; set; }
        public DateTime? CancelRequestedAt { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelRejectedAt { get; set; }
        public string? CancelRejectionReason { get; set; }
        public int? MentorWeeklyAvailabilityId { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ChangeRequestReason { get; set; }
        public DateTime? ChangeRequestedAt { get; set; }
        public string? ChangeResolution { get; set; }
    }
}
