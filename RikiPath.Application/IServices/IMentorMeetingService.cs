using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorMeetingService
{
    Task<ApiResponse<List<MentorMeetingPurchaseStatusResponse>>> GetMyPurchasesAsync(CancellationToken ct);
    Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken ct);
    #region Shared / General
    Task<ApiResponse<List<BookableMentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BookableMentorResponse>>> GetMentorsAsync(string? jlptLevel, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetPlansAsync(string? jlptLevel, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetAvailableSlotsAsync(DateTime from, DateTime to, int? mentorId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetAvailableSlotsAsync(DateTime from, DateTime to, int? mentorId, string? jlptLevel, CancellationToken cancellationToken = default);
    #endregion

    #region Learner Operations
    Task<ApiResponse<MentorMeetingPurchaseResponse>> PurchaseAsync(PurchaseMentorPlanRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> GetPurchaseStatusAsync(int subscriptionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> SyncPurchaseAsync(int subscriptionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> CompleteQaPaymentAsync(int subscriptionId, CancellationToken cancellationToken = default);
    #endregion

    #region Mentor Operations
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(CreateMentorAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> RequestCancelAvailabilitiesAsync(CancelAvailabilitiesRequest request, CancellationToken cancellationToken = default);
    #endregion

    #region Admin Operations
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetPendingAvailabilitiesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewCancelAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
    #endregion
}
