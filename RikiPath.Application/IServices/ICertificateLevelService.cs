using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateLevel;

namespace RikiPath.Application.IServices
{
    public interface ICertificateLevelService
    {
        Task<ApiResponse<List<CertificateLevelResponse>>> GetByCertificateTypeIdAsync(
            int certificateTypeId,
            CancellationToken cancellationToken = default);
    }
}
