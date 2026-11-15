using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

// Đánh giá và xếp hạng chất lượng buổi tư vấn Mentor 1-1 từ Học viên
public sealed class MentorReview
{
    public int Id { get; set; }

    // Khóa ngoại liên kết duy nhất với lịch hẹn đã hoàn thành (1 review / 1 booking)
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;

    // Chuyên gia được đánh giá
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;

    // Học viên thực hiện đánh giá
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;

    // Điểm đánh giá từ 1 đến 5 sao
    public int Rating { get; set; }

    // Nhận xét chi tiết từ học viên (tối đa 1000 ký tự)
    public string? Comment { get; set; }

    // Cờ hiển thị công khai trên hồ sơ chuyên gia
    public bool IsPublic { get; set; } = true;

    // Trạng thái kiểm duyệt nội dung
    public MentorReviewStatus ModerationStatus { get; set; } = MentorReviewStatus.Approved;

    // Phản hồi chính thức từ Mentor đối với đánh giá
    public string? MentorReply { get; set; }
    public DateTime? MentorRepliedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
