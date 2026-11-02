namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class PurchaseMentorPlanRequest
    {
        public int SubscriptionPlanId { get; set; }
        public string? FrontendOrigin { get; set; }
        public Guid? IdempotencyKey { get; set; }
    }
}
