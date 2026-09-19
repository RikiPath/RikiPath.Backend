using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class UserSubscriptionRepository(AppDbContext context) : GenericRepository<UserSubscription>(context), IUserSubscriptionRepository
    {
        public async Task<UserSubscription?> GetActiveSubscriptionAsync(int userId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.FirstOrDefaultAsync(us =>
             us.UserId == userId &&
             us.PaymentStatus == PaymentStatus.Paid &&
             (us.StartDate == null || us.StartDate <= now) &&
             (us.EndDate == null || us.EndDate >= now), ct);
        }
    }
}
