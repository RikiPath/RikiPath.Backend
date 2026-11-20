using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface ICohortEnrollmentEntitlementService
{
    Task<ApiResponse<object>> GrantAsync(int cohortId, Requests.StudyCourses.GrantCohortEntitlementRequest request, CancellationToken ct);
    Task<ApiResponse<object>> ListForCohortAsync(int cohortId, CancellationToken ct);
    Task<ApiResponse<object>> RevokeAsync(int entitlementId, Requests.StudyCourses.RevokeCohortEntitlementRequest request, CancellationToken ct);
    Task<ApiResponse<object>> MyEntitlementsAsync(CancellationToken ct);
}
