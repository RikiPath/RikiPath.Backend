using Microsoft.AspNetCore.Http;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IKanjiRecognitionService
{
    Task<ApiResponse<List<string>>> RecognizeAsync(IFormFile image, CancellationToken cancellationToken);
}
