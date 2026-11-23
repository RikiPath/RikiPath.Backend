using RikiPath.Application.DTOs.Dictionary;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IDictionaryLookupService
{
    Task<ApiResponse<DictionaryLookupResponse>> LookupAsync(
        int? userId,
        string? query,
        string? jlptLevel,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TranslateSelectionResponse>> TranslateAsync(
        int userId,
        TranslateSelectionRequest request,
        CancellationToken cancellationToken = default);
}
