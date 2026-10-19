using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ICertificateSectionRepository : IGenericRepository<CertificateSection>
    {
        Task<IReadOnlyList<CertificateSection>> GetForManagementAsync(
            int? certificateLevelId,
            bool includeArchived,
            CancellationToken cancellationToken = default);

        Task<CertificateSection?> GetForManagementByIdAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
