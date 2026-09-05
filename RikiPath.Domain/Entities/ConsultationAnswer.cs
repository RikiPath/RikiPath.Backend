using System;

namespace Domain.Entities
{
    /// <summary>The consultant's meeting notes or final written answer for a request.</summary>
    public class ConsultationAnswer : Base
    {
        public int Id { get; set; }

        public int ConsultationRequestId { get; set; }
        public ConsultationRequest ConsultationRequest { get; set; }
        public int ConsultantId { get; set; }
        public UserAccount Consultant { get; set; }

        public string? AnswerText { get; set; }
        public string? MeetingNotes { get; set; }
        public DateTime AnsweredAt { get; set; }
    }
}
