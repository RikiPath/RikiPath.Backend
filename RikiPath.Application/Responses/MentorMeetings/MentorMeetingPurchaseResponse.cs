namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorMeetingPurchaseResponse
    {
        public int SubscriptionId { get; set; }
        public int BookingId { get; set; }
        public long OrderCode { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int MeetingSessionCount { get; set; }
        public List<MentorMeetingFeatureResponse> Features { get; set; } = [];
        public decimal Amount { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string PaymentLinkId { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public DateTime? PaymentExpiresAt { get; set; }
    }
}
