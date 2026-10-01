using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface ISubscriptionPlanRepository : IGenericRepository<SubscriptionPlan>
    {
        Task<List<SubscriptionPlan>> GetMeetingPlansAsync(CancellationToken cancellationToken = default);
        Task<SubscriptionPlan?> GetMeetingPlanAsync(int id, CancellationToken cancellationToken = default);
    }
}
