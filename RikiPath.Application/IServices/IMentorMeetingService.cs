using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

/// <summary>
/// Interface quản lý logic và nghiệp vụ cho tính năng Mentor Meeting.
/// </summary>
public interface IMentorMeetingService
{
    Task<ApiResponse<List<MentorMeetingPurchaseStatusResponse>>> GetMyPurchasesAsync(CancellationToken ct);
    Task<ApiResponse<MentorBookingResponse>> BookFromBalanceAsync(BookIncludedMeetingRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken ct);
    #region Shared / General
    Task<ApiResponse<List<BookableMentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetAvailableSlotsAsync(DateTime from, DateTime to, int? mentorId, CancellationToken cancellationToken = default);
    #endregion

    #region Learner Operations
    Task<ApiResponse<MentorMeetingPurchaseResponse>> PurchaseAsync(PurchaseMentorPlanRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorBookingResponse>> BookIncludedSessionAsync(int subscriptionId, BookIncludedMeetingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> GetPurchaseStatusAsync(int subscriptionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> SyncPurchaseAsync(int subscriptionId, CancellationToken cancellationToken = default);
    #endregion

    #region Mentor Operations
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(CreateMentorAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> RequestCancelAvailabilitiesAsync(CancelAvailabilitiesRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorBookingResponse>>> GetMyMeetingsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorBookingResponse>> GetMentorBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorBookingResponse>> AcceptMentorBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    #endregion

    #region Admin Operations
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetPendingAvailabilitiesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewCancelAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
    #endregion
}
