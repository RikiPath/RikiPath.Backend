namespace RikiPath.Application.Requests.MentorMeetings;

public sealed class PurchaseMentorPlanRequest
{
    public int SubscriptionPlanId { get; set; }
    public int MentorAvailabilityId { get; set; }
}

public sealed class BookIncludedMeetingRequest
{
    public int MentorAvailabilityId { get; set; }
}

public sealed class CreateMentorAvailabilityRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
