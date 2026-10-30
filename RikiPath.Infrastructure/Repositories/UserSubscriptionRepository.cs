using RikiPath.Domain.Enums;
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

        public Task<UserSubscription?> GetMeetingPurchaseWithBookingsAsync(int subscriptionId, CancellationToken ct = default)
            => _dbSet
                .Include(x => x.SubscriptionPlan)
                .Include(x => x.MentorBookings)
                    .ThenInclude(x => x.MentorAvailability)
                .FirstOrDefaultAsync(x => x.Id == subscriptionId, ct);

        public Task<UserSubscription?> GetMeetingSubscriptionWithBookingsAsync(int subscriptionId, int userId, CancellationToken ct = default)
            => _dbSet
                .Include(x => x.SubscriptionPlan)
                .Include(x => x.MentorBookings!)
                    .ThenInclude(x => x.MentorAvailability).ThenInclude(x => x!.Mentor)
                .FirstOrDefaultAsync(x => x.Id == subscriptionId && x.UserId == userId && !x.IsDeleted, ct);

        public Task<UserSubscription?> GetQaPurchaseAsync(int userId, Guid idempotencyKey, CancellationToken ct = default)
        {
            var key = idempotencyKey.ToString("N");
            return _dbSet.Include(x => x.SubscriptionPlan)
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted &&
                    (x.PaymentTransactionId == $"QA_SIMULATED:PENDING:{key}" ||
                     x.PaymentTransactionId == $"QA_SIMULATED:{key}"), ct);
        }

        public async Task AcquireQaPurchaseLockAsync(int userId, Guid idempotencyKey, CancellationToken ct = default)
        {
            var keyBytes = idempotencyKey.ToByteArray();
            var lockKey = BitConverter.ToInt64(keyBytes, 0) ^ ((long)userId << 32);
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);
        }

        public Task<int> CompleteQaPaymentIfPendingAsync(
            int subscriptionId, string pendingMarker, string paidMarker, CancellationToken ct = default)
            => _dbSet.Where(x => x.Id == subscriptionId && !x.IsDeleted &&
                    x.PaymentStatus == PaymentStatus.Pending && x.PaymentTransactionId == pendingMarker)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PaymentStatus, PaymentStatus.Paid)
                    .SetProperty(x => x.PaymentTransactionId, paidMarker), ct);

        public Task<UserSubscription?> GetWithPlanAsync(int id, CancellationToken ct) =>
    _context.UserSubscriptions
        .Include(s => s.SubscriptionPlan)
        .FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task<DateTime?> GetActiveEndDateAsync(int userId, DateTime now, CancellationToken ct) =>
            await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                         && s.PaymentStatus == PaymentStatus.Paid
                         && s.EndDate > now)
                .MaxAsync(s => (DateTime?)s.EndDate, ct);
    }
}
