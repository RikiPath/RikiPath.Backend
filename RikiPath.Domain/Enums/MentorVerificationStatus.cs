namespace RikiPath.Domain.Enums
{
    // // Các trạng thái trong vòng đời xác minh hồ sơ Chuyên gia (Mentor).
    public enum MentorVerificationStatus
    {
        // Chưa nộp hồ sơ xác minh hoặc mới khởi tạo tài khoản.
        Unverified = 0,

        // Đã nộp hồ sơ, đang chờ Quản trị viên (Admin) xét duyệt.
        Pending = 1,

        // Hồ sơ đã được duyệt, chuyên gia đủ điều kiện nhận đặt lịch (Bookable).
        Approved = 2,

        // Hồ sơ bị từ chối phê duyệt kèm theo lý do cụ thể.
        Rejected = 3,

        // Quản trị viên yêu cầu bổ sung hoặc chỉnh sửa thông tin hồ sơ.
        ChangesRequested = 4
    }
}
