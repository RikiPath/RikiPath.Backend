using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Catalog;

namespace RikiPath.Application.IServices
{
    public interface IJlptCatalogService
    {
        Task<ApiResponse<List<JlptLevelCatalogResponse>>> GetLevelsAsync(CancellationToken cancellationToken = default);
    }
}
