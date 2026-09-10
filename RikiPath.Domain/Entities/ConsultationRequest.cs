using System;

using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// The meeting booking or written-answer ticket created from a purchase. For
    /// Meeting-type requests, <see cref="ConsultantAvailabilityId"/> links to the exact
    /// slot that was booked (1-to-1, both sides optional until a slot is actually chosen).
    /// </summary>
    public class ConsultationRequest : Base
    {
        public int Id { get; set; }

        public int ConsultationPurchaseId { get; set; }
        public ConsultationPurchase ConsultationPurchase { get; set; }

        public int? ConsultantId { get; set; }
        public UserAccount? Consultant { get; set; }

        public int? ConsultantAvailabilityId { get; set; }
        public ConsultantAvailability? ConsultantAvailability { get; set; }

        public ConsultationStatus Status { get; set; } = ConsultationStatus.PendingAssignment;
        public string? Question { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? MeetingLink { get; set; }
        public DateTime? CompletedAt { get; set; }

        public ConsultationAnswer? ConsultationAnswer { get; set; }
    }
}
