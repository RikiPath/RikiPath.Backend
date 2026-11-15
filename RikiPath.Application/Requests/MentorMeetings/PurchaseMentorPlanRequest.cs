namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class PurchaseMentorPlanRequest
    {
        public int SubscriptionPlanId { get; set; }
        public int? MentorAvailabilityId { get; set; }
        public string? Question { get; set; }
        public string? JlptLevel { get; set; }
        public string? FrontendOrigin { get; set; }
        public Guid? IdempotencyKey { get; set; }
    }
}
