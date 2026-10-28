using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateLevel;
using RikiPath.Application.Responses.CertificateType;
using System.Net;

namespace RikiPath.Application.Services
{
    public class CertificateTypeService(IUnitOfWork unitOfWork) : ICertificateTypeService
    {
        public async Task<ApiResponse<List<CertificateTypeResponse>>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var types = await unitOfWork.CertificateTypes
                    .FindAsync(t => t.IsActive, cancellationToken);

                var result = types
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Id)
                    .Select(t => new CertificateTypeResponse
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        SortOrder = t.SortOrder,
                    })
                    .ToList();

                return ApiResponse<List<CertificateTypeResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<CertificateTypeResponse>>.Fail(
                    "Không thể tải danh sách chứng chỉ.", HttpStatusCode.InternalServerError);
            }
        }
    }
}
