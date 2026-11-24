using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Sidebar;

namespace RikiPath.Application.IServices;

public interface ISidebarCounterService
{
    Task<ApiResponse<SidebarCountersResponse>> GetMyCountersAsync(CancellationToken cancellationToken = default);
}
