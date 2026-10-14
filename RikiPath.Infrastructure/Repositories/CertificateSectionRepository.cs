using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificateSectionRepository(AppDbContext context) : GenericRepository<CertificateSection>(context), ICertificateSectionRepository
    {
    }
}
