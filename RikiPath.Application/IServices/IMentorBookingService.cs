using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorBookingService
{
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyMentorBookingsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorBookingResponse>> GetMentorBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    // Đặt lịch tư vấn nguyên tử từ số dư tín dụng khả dụng của học viên (kèm tạo giữ chỗ và trừ vé FIFO).
    Task<ApiResponse<MentorBookingResponse>> BookSlotAsync(
        BookIncludedMeetingRequest request, CancellationToken cancellationToken = default);

    // Giải phóng giữ chỗ hoặc hủy lịch đặt (hoàn lại lượt tư vấn và phục hồi hiển thị khung giờ).
    Task<ApiResponse<MentorBookingResponse>> ReleaseBookingHoldAsync(
        int bookingId, string? reason = null, CancellationToken cancellationToken = default);
}
