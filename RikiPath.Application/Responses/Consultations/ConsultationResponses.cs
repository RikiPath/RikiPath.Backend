namespace RikiPath.Application.Responses.Consultations
{
    public class CreatePurchaseResponse
    {
        public int PurchaseId { get; set; }
        public long OrderCode { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public class BookingResponse
    {
        public int ConsultationRequestId { get; set; }
        public int ConsultantAvailabilityId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AvailableDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class SubmitTicketResponse
    {
        public int ConsultationRequestId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TicketQueueItem
    {
        public int ConsultationRequestId { get; set; }
        public int UserId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }

    public class AnswerTicketResponse
    {
        public int ConsultationRequestId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
        public DateTime AnsweredAt { get; set; }
    }
}
