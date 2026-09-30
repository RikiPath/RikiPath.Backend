using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ICertificateLevelRepository : IGenericRepository<CertificateLevel>
    {
        Task<IReadOnlyList<CertificateLevel>> GetLevelsByCertificateTypeIdAsync(int certificationId, CancellationToken ct = default);
    }
}
