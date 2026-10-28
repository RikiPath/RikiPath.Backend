using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateType;

namespace RikiPath.Application.IServices
{
    public interface ICertificateTypeService
    {
        Task<ApiResponse<List<CertificateTypeResponse>>> GetAllAsync(
           CancellationToken cancellationToken = default);
    }
}
