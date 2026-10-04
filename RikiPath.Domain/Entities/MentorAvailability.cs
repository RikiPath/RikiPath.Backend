using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace RikiPath.Domain.Entities
{
    public class MentorAvailability : Base
    {
        public const int DefaultMaxLearners = 4;

        public int Id { get; set; }

        public int MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public Guid RoomId { get; set; } = Guid.NewGuid();

        public int MaxLearners { get; set; } = DefaultMaxLearners;

        public int BookedCount { get; set; }

        public byte[]? RowVersion { get; set; }

        [NotMapped]
        public bool IsFull => BookedCount >= MaxLearners;

        public bool IsApproved { get; set; } = false;
        public string? AdminName { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }

        /// <summary>One slot, many bookings (one per learner).</summary>
        public ICollection<MentorBooking> MentorBookings { get; set; } = new List<MentorBooking>();
    }
}
