using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class ConsultationPackageRepository : GenericRepository<ConsultationPackage>, IConsultationPackageRepository
    {
        public ConsultationPackageRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<ConsultationPackage>> GetActivePackagesAsync()
            => await _context.ConsultationPackages
                .Where(x => x.IsActive)
                .OrderBy(x => x.Price)
                .ToListAsync();
    }
}
