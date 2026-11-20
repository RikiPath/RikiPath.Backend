using RikiPath.Application.Requests.AdminConfig;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConfig;

namespace RikiPath.Application.IServices;

public interface IAdminBusinessConfigService
{
    Task<ApiResponse<AdminSubscriptionPlansWorkspaceResponse>> GetSubscriptionPlansAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminSubscriptionPlanDto>> SaveSubscriptionPlanAsync(int? id, SaveSubscriptionPlanRequest request, int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<AdminPaymentListResponse>> GetPaymentsAsync(AdminPaymentQuery query, CancellationToken ct = default);
    Task<ApiResponse<AdminPaymentDetailDto>> GetPaymentDetailAsync(string stream, int id, CancellationToken ct = default);
    Task<ApiResponse<PayOsGatewayStatusDto>> GetPayOsStatusAsync(CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<AdminAuditLogDto>>> GetAuditLogsAsync(string? entityType, int? entityId, int take = 50, CancellationToken ct = default);
}
