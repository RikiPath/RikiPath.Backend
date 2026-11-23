using RikiPath.Application.DTOs.SrsTemplates;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public interface IContentPublishService
{
    Task<ApiResponse<List<AdminPublishCatalogItemResponse>>> GetCatalogAsync(
        string? entityType,
        ContentStatus? status,
        string? jlptLevel,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AdminPublishCatalogItemResponse>> TogglePublishStateAsync(
        int adminId,
        TogglePublishContentRequest request,
        CancellationToken cancellationToken = default);
}
