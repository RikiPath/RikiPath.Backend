using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorAvailabilityReviewService
{
    Task<ApiResponse<List<MentorAvailabilityResponse>>> GetSlotsAsync(CancellationToken cancellationToken);
    Task<ApiResponse<List<AdminMentorScheduleResponse>>> GetScheduleAsync(int mentorId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken);
}
