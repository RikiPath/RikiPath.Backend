using RikiPath.Application.Responses;
using RikiPath.Application.Responses.JapaneseDictionary;

namespace RikiPath.Application.IServices;

public interface IJapaneseDictionaryService
{
    Task<ApiResponse<List<JapaneseDictionaryEntryResponse>>> SearchAsync(
        string readingKana,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<JapaneseDictionaryEntryResponse>> AddPersonalAsync(
        CreatePersonalDictionaryEntryRequest request,
        CancellationToken cancellationToken = default);
}
