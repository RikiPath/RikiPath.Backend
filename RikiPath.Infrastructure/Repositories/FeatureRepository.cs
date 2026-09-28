using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class FeatureRepository(AppDbContext context) : GenericRepository<Feature>(context), IFeatureRepository
    {
    }
}
