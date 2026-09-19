using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificationRepository(AppDbContext context) : GenericRepository<Certification>(context), ICertificationRepository
    {
    }
}
