using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IJapaneseDictionaryService
{
    Task<ApiResponse<List<JapaneseDictionaryCandidate>>> SearchAsync(
        string? readingKana, CancellationToken cancellationToken = default);
}
