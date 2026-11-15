using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorCancellationService
{
    // Hủy lịch hẹn tư vấn có hỗ trợ phân tầng chính sách (>= 24h vs < 24h), hoàn vé tự động và hoàn trả slot
    Task<ApiResponse<MentorCancellationResponse>> CancelBookingAsync(
        int bookingId,
        CancelMentorBookingRequest request,
        CancellationToken cancellationToken = default);

    // Dời lịch hẹn tư vấn sang một khung giờ khả dụng khác của cùng một Mentor (chỉ áp dụng trước >= 24h)
    Task<ApiResponse<MentorRescheduleResponse>> RescheduleBookingAsync(
        int bookingId,
        RescheduleMentorBookingRequest request,
        CancellationToken cancellationToken = default);

    // Kiểm tra điều kiện và quyền lợi hoàn tiền/dời lịch theo chính sách thời gian thực
    Task<ApiResponse<CancellationPolicyEligibilityResponse>> CheckCancellationEligibilityAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    // Quản trị viên duyệt hoặc từ chối yêu cầu ngoại lệ hoàn vé cho các ca hủy sát giờ (< 24h)
    Task<ApiResponse<MentorCancellationResponse>> ReviewExceptionRequestAsync(
        int bookingId,
        ReviewCancellationExceptionRequest request,
        CancellationToken cancellationToken = default);
}
