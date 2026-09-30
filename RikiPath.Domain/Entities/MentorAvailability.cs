using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>A mentor's open meeting slot. IsBooked mirrors whether a MentorBooking now references it.</summary>
    public class MentorAvailability : Base
    {
        public int Id { get; set; }

        public int MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }

        public MentorBooking? MentorBooking { get; set; }
    }
}
