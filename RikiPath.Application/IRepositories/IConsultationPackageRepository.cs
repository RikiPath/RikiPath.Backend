using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IConsultationPackageRepository : IGenericRepository<ConsultationPackage>
    {
        Task<List<ConsultationPackage>> GetActivePackagesAsync();
    }
}
