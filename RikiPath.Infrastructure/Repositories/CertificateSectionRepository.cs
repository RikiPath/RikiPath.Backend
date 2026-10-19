using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure.Repositories
{
    public class CertificateSectionRepository(AppDbContext context) : GenericRepository<CertificateSection>(context), ICertificateSectionRepository
    {
        public async Task<IReadOnlyList<CertificateSection>> GetForManagementAsync(
            int? certificateLevelId,
            bool includeArchived,
            CancellationToken cancellationToken = default)
        {
            var query = _dbSet
                .AsNoTracking()
                .Include(section => section.CertificateLevel)
                .ThenInclude(level => level.CertificateType)
                .AsQueryable();

            if (!includeArchived)
                query = query.Where(section => !section.IsDeleted);

            if (certificateLevelId.HasValue)
                query = query.Where(section => section.CertificateLevelId == certificateLevelId.Value);

            return await query
                .OrderBy(section => section.CertificateLevel.SortOrder)
                .ThenBy(section => section.SortOrder)
                .ThenBy(section => section.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<CertificateSection?> GetForManagementByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
            => await _dbSet
                .AsNoTracking()
                .Include(section => section.CertificateLevel)
                .ThenInclude(level => level.CertificateType)
                .FirstOrDefaultAsync(section => section.Id == id, cancellationToken);
    }
}
