using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateLevel;
using System.Net;

namespace RikiPath.Application.Services
{
    public class CertificateLevelService(IUnitOfWork unitOfWork) : ICertificateLevelService
    {
        public async Task<ApiResponse<List<CertificateLevelResponse>>> GetByCertificateTypeIdAsync(
           int certificateTypeId,
           CancellationToken cancellationToken = default)
        {
            try
            {
                if (certificateTypeId <= 0)
                    return ApiResponse<List<CertificateLevelResponse>>.Fail(
                        "Loại chứng chỉ không hợp lệ.", HttpStatusCode.BadRequest);

                // CertificateType vẫn có IsActive nên lọc ở đây
                var typeExists = await unitOfWork.CertificateTypes
                    .AnyAsync(t => t.Id == certificateTypeId && t.IsActive, cancellationToken);
                if (!typeExists)
                    return ApiResponse<List<CertificateLevelResponse>>.NotFound(
                        "Không tìm thấy loại chứng chỉ.");

                var levels = await unitOfWork.CertificateLevels
                    .FindAsync(l => l.CertificateTypeId == certificateTypeId, cancellationToken);

                var result = levels
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new CertificateLevelResponse
                    {
                        Id = l.Id,
                        CertificateTypeId = l.CertificateTypeId,
                        Code = l.Code,
                        Description = l.Description,
                        SortOrder = l.SortOrder,
                    })
                    .ToList();

                return ApiResponse<List<CertificateLevelResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<CertificateLevelResponse>>.Fail(
                    "Không thể tải danh sách cấp độ.", HttpStatusCode.InternalServerError);
            }
        }
    }
}
