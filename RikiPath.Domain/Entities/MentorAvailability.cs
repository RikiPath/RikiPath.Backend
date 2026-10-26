using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace RikiPath.Domain.Entities
{
    public class MentorAvailability : Base
    {
        public const int DefaultMaxLearners = 1;

        public int? MentorWeeklyAvailabilityId { get; set; }
        public MentorWeeklyAvailability? WeeklyAvailability { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ChangeRequestReason { get; set; }
        public DateTime? ChangeRequestedAt { get; set; }
        public string? ChangeResolution { get; set; }

        public int Id { get; set; }

        public int MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public Guid RoomId { get; set; } = Guid.NewGuid();

        public int MaxLearners { get; set; } = DefaultMaxLearners;

        public int BookedCount { get; set; }

        public byte[]? RowVersion { get; set; }
        public uint Version { get; set; }

        [NotMapped]
        public bool IsFull => BookedCount >= MaxLearners;

        public bool IsApproved { get; set; } = false;
        public string? AdminName { get; set; }
        public DateTime? CancelRequestedAt { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelRejectedAt { get; set; }
        public string? CancelRejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }

        /// <summary>New scheduled slots allow one learner; historical group slots are preserved.</summary>
        public ICollection<MentorBooking> MentorBookings { get; set; } = new List<MentorBooking>();
    }
}
