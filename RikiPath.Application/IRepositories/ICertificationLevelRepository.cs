using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ICertificationLevelRepository : IGenericRepository<CertificationLevel>
    {
        Task<IReadOnlyList<CertificationLevel>> GetLevelsByCertificationIdAsync(int certificationId, CancellationToken ct = default);
    }
}
