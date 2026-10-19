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

        public async Task<IReadOnlyList<CertificateLevel>> GetLearnerVisibleJlptLevelsAsync(CancellationToken ct = default)
        {
            // Chỉ trả các cấp độ JLPT khả dụng cho luồng Learner.
            return await _dbSet
                .AsNoTracking()
                .Where(level =>
                    !level.IsDeleted
                    && !level.CertificateType.IsDeleted
                    && level.CertificateType.IsActive
                    && level.CertificateType.Name == "JLPT")
                // Sắp xếp catalog ổn định để Mobile không phụ thuộc vào thứ tự bản ghi trong CSDL.
                .OrderBy(level => level.SortOrder)
                .ThenBy(level => level.Id)
                .ToListAsync(ct);
        }

        public async Task<CertificateLevel?> GetActiveJlptLevelByIdAsync(
            int id,
            CancellationToken ct = default)
            => await _dbSet
                .AsNoTracking()
                .Include(level => level.CertificateType)
                .FirstOrDefaultAsync(
                    level => level.Id == id
                        && !level.IsDeleted
                        && !level.CertificateType.IsDeleted
                        && level.CertificateType.IsActive
                        && level.CertificateType.Name == "JLPT",
                    ct);
    }
}
