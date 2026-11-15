using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorQuality;

// Thông tin chi tiết một đánh giá chất lượng tư vấn Mentor
public sealed class MentorReviewResponse
{
    public int Id { get; set; }
    public int MentorBookingId { get; set; }
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public int LearnerId { get; set; }
    public string LearnerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsPublic { get; set; }
    public MentorReviewStatus ModerationStatus { get; set; }
    public string? MentorReply { get; set; }
    public DateTime? MentorRepliedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

// Bảng tổng hợp xếp hạng và thống kê sao của Chuyên gia tư vấn
public sealed class MentorRatingSummaryResponse
{
    public int MentorId { get; set; }
    public int TotalReviews { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<int, int> RatingBreakdown { get; set; } = new();
    public double PositiveRatePercent { get; set; }
}
