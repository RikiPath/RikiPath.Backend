using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class MentorBooking : Base
    {
        public int Id { get; set; }

        public int UserSubscriptionId { get; set; }
        public UserSubscription UserSubscription { get; set; } = null!;

        public int? MentorAvailabilityId { get; set; }
        public MentorAvailability? MentorAvailability { get; set; }

        public MentorStatus Status { get; set; } = MentorStatus.PendingAssignment;
        public string? Question { get; set; }
        public DateTime? ScheduledAt { get; set; }

        /// <summary>Filled when the learner books: URL that contains the slot's RoomId.</summary>
        public string? MeetingLink { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Note? Note { get; set; }
    }
}
