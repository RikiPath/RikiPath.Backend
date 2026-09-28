using System;

using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// The meeting booking or written-answer ticket created from a purchase. For
    /// Meeting-type requests, <see cref="MentorAvailabilityId"/> links to the exact
    /// slot that was booked (1-to-1, both sides optional until a slot is actually chosen).
    /// </summary>
    public class MentorBooking : Base
    {
        public int Id { get; set; }

        public int UserSubscriptionId { get; set; }
        public UserSubscription UserSubscription { get; set; } = null!;

        public int? MentorAvailabilityId { get; set; }
        public MentorAvailability? MentorAvailability { get; set; }

        public ConsultationStatus Status { get; set; } = ConsultationStatus.PendingAssignment;
        public string? Question { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? MeetingLink { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Note? Note { get; set; }
    }
}
