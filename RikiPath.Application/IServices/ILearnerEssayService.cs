using Microsoft.AspNetCore.Http;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Essay;

namespace RikiPath.Application.IServices;

public interface ILearnerEssayService
{
    Task<ApiResponse<EssayScanResponse>> ScanAsync(IFormFile image, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<EssayListItemResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<EssayDetailResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<EssayDetailResponse>> UpdateAsync(int id, string contentText, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<EssayScanResponse>> RescanAsync(int id, IFormFile image, CancellationToken cancellationToken = default);
}
