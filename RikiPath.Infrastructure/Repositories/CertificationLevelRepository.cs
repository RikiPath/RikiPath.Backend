using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificationLevelRepository(AppDbContext context) : GenericRepository<CertificationLevel>(context), ICertificationLevelRepository
    {
        public async Task<IReadOnlyList<CertificationLevel>> GetLevelsByCertificationIdAsync(int certificationId, CancellationToken ct = default)
        {
            return await _dbSet.Where(cl => cl.CertificationId == certificationId)
                               .OrderBy(cl => cl.SortOrder)
                               .ToListAsync(ct);
        }
    }
}
