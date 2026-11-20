using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificateTypeRepository(AppDbContext context) : GenericRepository<CertificateType>(context), ICertificateTypeRepository
    {
        public async Task LockOrderScopeAsync(CancellationToken ct = default)
        {
            if (_context.Database.IsNpgsql())
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({"catalog:certificate-types"}, 0))", ct);
        }
    }
}
