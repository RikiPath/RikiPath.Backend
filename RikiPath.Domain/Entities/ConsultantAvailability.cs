using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>A consultant's open meeting slot. IsBooked mirrors whether a ConsultationRequest now references it.</summary>
    public class ConsultantAvailability : Base
    {
        public int Id { get; set; }

        public int ConsultantId { get; set; }
        public UserAccount Consultant { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }

        public ConsultationRequest? ConsultationRequest { get; set; }
    }
}
