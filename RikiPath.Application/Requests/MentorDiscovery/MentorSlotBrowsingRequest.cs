namespace RikiPath.Application.Requests.MentorDiscovery;

public sealed class MentorSlotBrowsingRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
}
