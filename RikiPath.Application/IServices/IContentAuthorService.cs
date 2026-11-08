using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.IServices;

public interface IContentAuthorService
{
    Task<ApiResponse<ContentReviewStatusResponse>> SaveAsync(ContentEntityType type, int? id, AuthorContentRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AuthorContentDetailsResponse>> GetByIdAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
    Task<ApiResponse<AuthorContentDetailsResponse>> GetForAdminAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
    Task<ApiResponse> DeleteAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
}
