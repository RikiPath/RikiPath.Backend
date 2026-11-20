using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ICertificateTypeRepository : IGenericRepository<CertificateType>
    {
        Task LockOrderScopeAsync(CancellationToken ct = default);
    }
}
