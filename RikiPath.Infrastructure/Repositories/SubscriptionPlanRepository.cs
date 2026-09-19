using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class SubscriptionPlanRepository(AppDbContext context) : GenericRepository<SubscriptionPlan>(context), ISubscriptionPlanRepository
    {
    }
}
