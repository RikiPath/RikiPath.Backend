using RikiPath.Application.Requests.CertificateSections;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateSections;

namespace RikiPath.Application.IServices
{
    public interface IAdminCertificateSectionService
    {
        Task<ApiResponse<List<CertificateSectionResponse>>> GetAllAsync(
            int? certificateLevelId,
            bool includeArchived,
            CancellationToken cancellationToken);

        Task<ApiResponse<CertificateSectionResponse>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<ApiResponse<CertificateSectionResponse>> CreateAsync(
            CreateCertificateSectionRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<CertificateSectionResponse>> UpdateAsync(
            int id,
            UpdateCertificateSectionRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<CertificateSectionResponse>> ArchiveAsync(
            int id,
            CancellationToken cancellationToken);
    }
}
