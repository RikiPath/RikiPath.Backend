using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class SubscriptionPlanRepository(AppDbContext context) : GenericRepository<SubscriptionPlan>(context), ISubscriptionPlanRepository
    {
        public Task<List<SubscriptionPlan>> GetMeetingPlansAsync(CancellationToken cancellationToken = default)
            => _dbSet.AsNoTracking().Include(x => x.Features)
                .Where(x => x.IsActive && !x.IsDeleted && x.MeetingSessionCount > 0)
                .OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);

        public Task<SubscriptionPlan?> GetMeetingPlanAsync(int id, CancellationToken cancellationToken = default)
            => _dbSet.Include(x => x.Features)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted && x.MeetingSessionCount > 0, cancellationToken);
    }
}
