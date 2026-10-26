using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
namespace RikiPath.Application.IServices;
public interface IMentorPlanService
{
    Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetAsync(CancellationToken ct);
    Task<ApiResponse<int>> SaveAsync(int? id, SaveMentorPlanRequest request, CancellationToken ct);
}
