using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorMeetingService
{
    Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetAvailableSlotsAsync(DateTime from, DateTime to, int? mentorId, CancellationToken cancellationToken);
    Task<ApiResponse<MentorMeetingPurchaseResponse>> PurchaseAsync(PurchaseMentorPlanRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<MentorBookingResponse>> BookIncludedSessionAsync(int subscriptionId, BookIncludedMeetingRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken);
    Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> GetPurchaseStatusAsync(int subscriptionId, CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken);
    Task<ApiResponse<MentorAvailabilityResponse>> CreateAvailabilityAsync(CreateMentorAvailabilityRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyMeetingsAsync(CancellationToken cancellationToken);
}
