using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IServices;

public interface IMentorGroupPolicyService
{
    Task<MentorGroupPolicy?> GetEffectiveAsync(DateTime sessionStartUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MentorGroupPolicy>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorGroupPolicy>> CreateVersionAsync(
        CreateMentorGroupPolicyRequest request, int adminId, CancellationToken cancellationToken = default);
}
