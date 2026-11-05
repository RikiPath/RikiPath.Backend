namespace RikiPath.Application.Responses.MentorQuality;

// Chỉ số chất lượng và thống kê hoạt động của Chuyên gia tư vấn
public sealed class MentorQualityMetricResponse
{
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public int CompletedSessionsCount { get; set; }
    public int CancelledSessionsCount { get; set; }
    public int TotalReviewsCount { get; set; }
    public double AverageRating { get; set; }
    public int TotalDisputesCount { get; set; }
    public double DisputeRatePercent { get; set; }
    public int ResolvedRefundCount { get; set; }
}
