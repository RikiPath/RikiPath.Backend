using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    // // Hồ sơ chuyên môn và thông tin xác minh của Chuyên gia tư vấn (Mentor).
    // Quan hệ 1-1 với tài khoản người dùng UserAccount (Role = Mentor).
    public class MentorProfile : Base
    {
        // ID định danh chuyên gia (khóa chính và khóa ngoại tham chiếu Users.Id).
        public int MentorId { get; set; }

        // Tài khoản người dùng liên kết.
        public UserAccount Mentor { get; set; } = null!;

        // Giới thiệu bản thân và phương pháp giảng dạy.
        public string? Bio { get; set; }

        // Các lĩnh vực chuyên môn (JLPT, Kaiwa, Tiếng Nhật thương mại,...).
        public string? Expertise { get; set; }

        // Trình độ chứng chỉ JLPT cao nhất (ví dụ: N1, N2).
        public string? JlptLevel { get; set; }

        // Số năm kinh nghiệm giảng dạy hoặc làm việc thực tế.
        public int? YearsOfExperience { get; set; }

        // Nơi công tác, đơn vị đào tạo hoặc doanh nghiệp hiện tại/gần nhất.
        public string? Workplace { get; set; }

        // Đường dẫn tài liệu chứng chỉ, bằng cấp phục vụ xác minh.
        public string? VerificationDocumentUrl { get; set; }

        // Đường dẫn video hoặc clip giới thiệu bản thân.
        public string? IntroductionVideoUrl { get; set; }

        // Trạng thái phê duyệt hồ sơ chuyên gia.
        public MentorVerificationStatus VerificationStatus { get; set; } = MentorVerificationStatus.Unverified;

        // Thời điểm nộp hồ sơ xin xét duyệt gần nhất.
        public DateTime? SubmittedAt { get; set; }

        // Thời điểm Quản trị viên xử lý xét duyệt.
        public DateTime? ReviewedAt { get; set; }

        // Tên hoặc định danh Quản trị viên đã thực hiện xét duyệt.
        public string? ReviewedByAdminName { get; set; }

        // Lý do từ chối nếu hồ sơ không đạt yêu cầu.
        public string? RejectionReason { get; set; }

        // Ghi chú của Quản trị viên hoặc nội dung yêu cầu bổ sung thông tin.
        public string? ReviewNotes { get; set; }
    }
}
