namespace RikiPath.Application.Responses.MentorDiscovery;

public sealed class PublicMentorSlotsGroupedResponse
{
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TotalAvailableSlots { get; set; }
    public List<PublicMentorDailySlotsResponse> DailySlots { get; set; } = new();
}
