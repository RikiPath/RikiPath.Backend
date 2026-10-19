using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Catalog;
using System.Net;

namespace RikiPath.Application.Services
{
    public sealed class JlptCatalogService(IUnitOfWork unitOfWork) : IJlptCatalogService
    {
        public async Task<ApiResponse<List<JlptLevelCatalogResponse>>> GetLevelsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var levels = await unitOfWork.CertificateLevels
                    .GetLearnerVisibleJlptLevelsAsync(cancellationToken);
                var response = levels
                    .Select(level => new JlptLevelCatalogResponse
                    {
                        Id = level.Id,
                        Code = level.Code,
                        Description = level.Description,
                        SortOrder = level.SortOrder,
                    })
                    .ToList();

                return ApiResponse<List<JlptLevelCatalogResponse>>.Success(response);
            }
            catch
            {
                return ApiResponse<List<JlptLevelCatalogResponse>>.Fail(
                    "Không thể tải danh mục cấp độ JLPT.",
                    HttpStatusCode.InternalServerError);
            }
        }
    }
}
