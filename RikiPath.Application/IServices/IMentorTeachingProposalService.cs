using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;

namespace RikiPath.Application.IServices;

public interface IMentorTeachingProposalService
{
    Task<ApiResponse<MentorTeachingProposalContextResponse>> GetMentorContextAsync(CancellationToken ct = default);
    Task<ApiResponse<List<TeachingProposalResponse>>> GetMyProposalsAsync(string? status = null, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> GetMyProposalByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> CreateDraftAsync(SaveTeachingProposalDraftRequest request, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> UpdateDraftAsync(int id, SaveTeachingProposalDraftRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteDraftAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalValidationResponse>> ValidateDraftAsync(ValidateTeachingProposalRequest request, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> SubmitProposalAsync(int id, SubmitTeachingProposalRequest request, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> ResubmitProposalAsync(int id, ResubmitTeachingProposalRequest request, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> WithdrawProposalAsync(int id, WithdrawTeachingProposalRequest? request, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> ReopenForRevisionAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<ProposalAttachmentDto>> GetAuthorizedAttachmentAsync(int proposalId, int attachmentIndex, bool isAdmin, CancellationToken ct = default);

    // Admin endpoints
    Task<ApiResponse<List<TeachingProposalResponse>>> GetAdminProposalsAsync(string? status = null, int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> GetAdminProposalByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<TeachingProposalResponse>> ReviewProposalAsync(int id, ReviewTeachingProposalRequest request, CancellationToken ct = default);
    Task<ApiResponse<BulkApproveTeachingProposalsResponse>> PrevalidateBulkApproveAsync(BulkApproveTeachingProposalsRequest request, CancellationToken ct = default);
    Task<ApiResponse<BulkApproveTeachingProposalsResponse>> BulkApproveProposalsAsync(BulkApproveTeachingProposalsRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<TeachingProposalResponse>>> GetApprovedProposalsForCohortAsync(int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default);
    Task<ApiResponse<RepairApprovedCohortsResponse>> RepairApprovedCohortsAsync(CancellationToken ct = default);
    Task<RepairApprovedCohortsResponse> BackfillApprovedProposalCohortsSystemAsync(CancellationToken ct = default);
    Task<ApiResponse<RepairInvalidProposalSchedulesResponse>> RepairInvalidNeedsRevisionSchedulesAsync(CancellationToken ct = default);
}
