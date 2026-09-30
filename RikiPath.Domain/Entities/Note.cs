using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>The mentor's meeting notes or final written answer for a request.</summary>
    public class Note : Base
    {
        public int Id { get; set; }

        public int MentorBookingId { get; set; }
        public MentorBooking MentorBooking { get; set; }
        public int MentorId { get; set; }
        public UserAccount Mentor { get; set; }

        public string? AnswerText { get; set; }
        public string? MeetingNotes { get; set; }
        public DateTime AnsweredAt { get; set; }
    }
}
