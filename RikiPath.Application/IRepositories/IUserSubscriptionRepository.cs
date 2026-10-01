using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IUserSubscriptionRepository : IGenericRepository<UserSubscription>
    {
        Task<UserSubscription?> GetActiveSubscriptionAsync(int userId, CancellationToken ct = default);
        Task<UserSubscription?> GetMeetingPurchaseWithBookingsAsync(int subscriptionId, CancellationToken ct = default);
        Task<UserSubscription?> GetMeetingSubscriptionWithBookingsAsync(int subscriptionId, int userId, CancellationToken ct = default);
    }
}
