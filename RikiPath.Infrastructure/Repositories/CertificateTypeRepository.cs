using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificateTypeRepository(AppDbContext context) : GenericRepository<CertificateType>(context), ICertificateTypeRepository
    {
    }
}
