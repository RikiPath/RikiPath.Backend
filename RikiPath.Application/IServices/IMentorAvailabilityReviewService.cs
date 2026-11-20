using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorAvailabilityReviewService
{
    Task<ApiResponse<List<MentorAvailabilityResponse>>> SplitAsync(int id, int sessionMinutes, CancellationToken ct);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetSlotsAsync(CancellationToken cancellationToken);
    Task<ApiResponse<List<AdminMentorScheduleResponse>>> GetScheduleAsync(int mentorId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedMentorAvailabilityResponse>> QueryMyAvailabilityAsync(MentorAvailabilityQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(CreateMentorAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityBatchAsync(CreateMentorAvailabilityBatchRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> RequestCancelAvailabilitiesAsync(CancelAvailabilitiesRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetPendingAvailabilitiesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ReviewCancelAvailabilitiesAsync(List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default);
}
