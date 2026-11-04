using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorBookingResponse
    {
        public int Id { get; set; }
        public int UserSubscriptionId { get; set; }
        public long? CreditGrantId { get; set; }
        public int? MentorAvailabilityId { get; set; }
        public int? MentorId { get; set; }
        public string? MentorName { get; set; }
        public int? LearnerId { get; set; }
        public string? LearnerName { get; set; }
        public int? CompensationLevelId { get; set; }
        public int? CertificateLevelId
        {
            get => CompensationLevelId;
            set => CompensationLevelId = value;
        }
        public string? JlptLevel { get; set; }
        public MentorStatus Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? MeetingLink { get; set; }
        public Guid? RoomId { get; set; }
        public string? Question { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
