using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificateLevelRepository(AppDbContext context) : GenericRepository<CertificateLevel>(context), ICertificateLevelRepository
    {
        public async Task<IReadOnlyList<CertificateLevel>> GetLevelsByCertificateTypeIdAsync(int certificationId, CancellationToken ct = default)
        {
            return await _dbSet.Where(cl => cl.CertificateTypeId == certificationId)
                               .OrderBy(cl => cl.SortOrder)
                               .ToListAsync(ct);
        }
    }
}
