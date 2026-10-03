

using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorBookingResponse
    {
        public int Id { get; set; }
        public int UserSubscriptionId { get; set; }
        public int? MentorAvailabilityId { get; set; }
        public int? MentorId { get; set; }
        public string? MentorName { get; set; }
        public int? LearnerId { get; set; }
        public string? LearnerName { get; set; }
        public MentorStatus Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? MeetingLink { get; set; }
        public Guid? RoomId { get; set; }
    }
}
