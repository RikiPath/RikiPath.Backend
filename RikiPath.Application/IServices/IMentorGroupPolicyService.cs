using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IServices;

public interface IMentorGroupPolicyService
{
    Task<MentorGroupPolicy?> GetEffectiveAsync(DateTime sessionStartUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MentorGroupPolicy>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportedPolicyLevelDto>> GetSupportedLevelsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorGroupPolicy>> CreateVersionAsync(
        CreateMentorGroupPolicyRequest request, int adminId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorGroupPolicy>> UpdateVersionAsync(
        int policyId, UpdateMentorGroupPolicyRequest request, int adminId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorGroupPolicy>> UpdateSectionAsync(
        int policyId, string sectionKey, UpdateMentorGroupPolicySectionRequest request, int adminId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MentorGroupPolicy>> ActivateVersionAsync(
        int policyId, int adminId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteVersionAsync(
        int policyId, int adminId, CancellationToken cancellationToken = default);
}
