using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IAdminDashboardService
{
    Task<ApiResponse<AdminDashboardResponse>> GetAsync(CancellationToken cancellationToken);
}
