namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class PurchaseMentorPlanRequest
    {
        public int SubscriptionPlanId { get; set; }
        public int MentorAvailabilityId { get; set; }
    }
}
