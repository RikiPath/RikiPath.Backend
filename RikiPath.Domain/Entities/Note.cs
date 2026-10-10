using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>The mentor's meeting notes or final written answer for a request.</summary>
    public class Note : Base
    {
        public int Id { get; set; }

        public int MentorBookingId { get; set; }
        public MentorBooking MentorBooking { get; set; }
        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public string AuthorRole { get; set; } = string.Empty;
        public string? Content { get; set; }
        public DateTime AnsweredAt { get; set; }
    }
}
