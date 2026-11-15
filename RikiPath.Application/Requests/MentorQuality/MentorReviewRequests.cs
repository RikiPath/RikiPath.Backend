using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.MentorQuality;

// Yêu cầu tạo mới đánh giá cho buổi tư vấn Mentor 1-1 đã hoàn thành
public sealed class CreateMentorReviewRequest
{
    public int MentorBookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

// Yêu cầu cập nhật nội dung đánh giá của chính học viên
public sealed class UpdateMentorReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

// Chuyên gia tư vấn gửi phản hồi cho đánh giá của học viên
public sealed class MentorReplyReviewRequest
{
    public string Reply { get; set; } = string.Empty;
}

// Quản trị viên kiểm duyệt cờ hiển thị và trạng thái đánh giá
public sealed class ModerateMentorReviewRequest
{
    public bool IsPublic { get; set; }
    public MentorReviewStatus Status { get; set; } = MentorReviewStatus.Approved;
}
