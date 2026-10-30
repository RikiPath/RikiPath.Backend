using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorMeetingPurchaseStatusResponse
    {
        public int SubscriptionId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public PaymentStatus PaymentStatus { get; set; }
        public bool IsCheckoutExpired { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MentorId { get; set; }
        public int MeetingSessionsIncluded { get; set; }
        public int MeetingSessionsUsed { get; set; }
        public List<MentorBookingResponse> Bookings { get; set; } = [];
        public string? TransactionId { get; set; }
        public bool IsQaSimulated { get; set; }
        public string? DemoQrPayload { get; set; }
    }
}
