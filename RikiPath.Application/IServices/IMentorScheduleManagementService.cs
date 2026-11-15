using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;

namespace RikiPath.Application.IServices
{
    // // Giao diện dịch vụ quản lý mẫu lịch làm việc, quy tắc ca và sinh khung giờ tự động cho Chuyên gia.
    public interface IMentorScheduleManagementService
    {
        // Lấy mẫu lịch làm việc hiện tại của chuyên gia đang đăng nhập.
        Task<ApiResponse<MentorScheduleResponse>> GetMyScheduleAsync(CancellationToken cancellationToken = default);

        // Cập nhật hoặc tạo mới mẫu lịch làm việc và các quy tắc ca lặp của chuyên gia.
        Task<ApiResponse<MentorScheduleResponse>> UpdateMyScheduleAsync(UpdateMentorScheduleRequest request, CancellationToken cancellationToken = default);

        // Lấy danh sách các quy tắc ca làm việc của chuyên gia đang đăng nhập.
        Task<ApiResponse<List<MentorScheduleRuleResponse>>> GetMyScheduleRulesAsync(CancellationToken cancellationToken = default);

        // Cập nhật danh sách quy tắc ca làm việc lặp theo tuần của chuyên gia.
        Task<ApiResponse<List<MentorScheduleRuleResponse>>> UpdateMyScheduleRulesAsync(List<MentorScheduleRuleRequest> rules, CancellationToken cancellationToken = default);

        // Xem trước các khung giờ 90 phút (kèm 15 phút nghỉ) sẽ được sinh ra trong khoảng thời gian.
        Task<ApiResponse<SlotPreviewResponse>> PreviewSlotsAsync(PreviewSlotsRequest request, CancellationToken cancellationToken = default);

        // Sinh và công bố chính thức các khung giờ làm việc khả dụng vào bảng MentorAvailabilities.
        Task<ApiResponse<PublishSlotsResponse>> PublishSlotsAsync(PublishSlotsRequest request, CancellationToken cancellationToken = default);

        // Hủy hoặc đóng khung giờ làm việc tương lai chưa có học viên đặt chỗ.
        Task<ApiResponse<bool>> DisableSlotAsync(int slotId, DisableSlotRequest? request = null, CancellationToken cancellationToken = default);

        // Dành cho Quản trị viên: Xem chi tiết mẫu lịch làm việc của một chuyên gia bất kỳ theo ID.
        Task<ApiResponse<MentorScheduleResponse>> GetMentorScheduleByIdForAdminAsync(int mentorId, CancellationToken cancellationToken = default);
    }
}
