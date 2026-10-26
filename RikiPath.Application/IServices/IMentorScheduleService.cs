using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
namespace RikiPath.Application.IServices;
public interface IMentorScheduleService
{
    Task<ApiResponse<List<WeeklyAvailabilityResponse>>> GetWeekAsync(DateOnly weekStart, CancellationToken ct);
    Task<ApiResponse<bool>> SaveWeekAsync(SaveWeeklyAvailabilityRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> AssignAsync(AssignMentorSlotsRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> PublishAsync(DateOnly weekStart, CancellationToken ct);
    Task<ApiResponse<bool>> RequestChangeAsync(int slotId, string reason, CancellationToken ct);
    Task<ApiResponse<bool>> ResolveChangeAsync(int slotId, ResolveMentorSlotRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> CancelBookingAsync(int bookingId, CancellationToken ct);
}
