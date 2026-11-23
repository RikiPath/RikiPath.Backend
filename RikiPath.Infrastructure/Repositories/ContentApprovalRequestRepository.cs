using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    // Cùng giả định với MeetingNoteRepository: GenericRepository<T> nhận AppDbContext qua constructor.
    public sealed class ContentApprovalRequestRepository(AppDbContext context)
        : GenericRepository<ContentApprovalRequest>(context), IContentApprovalRequestRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<ContentApprovalRequest?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken)
        {
            var row = _context.Database.IsNpgsql()
                ? await _context.Set<ContentApprovalRequest>()
                    .FromSqlInterpolated($"SELECT * FROM \"ContentApprovalRequests\" WHERE \"Id\" = {id} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken)
                : await _context.Set<ContentApprovalRequest>()
                    .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

            // Reload after acquiring row lock so a concurrent review cannot reuse stale tracked state.
            if (row is not null && _context.Database.IsNpgsql())
                await _context.Entry(row).ReloadAsync(cancellationToken);
            return row;
        }

        public async Task<IReadOnlyList<ContentApprovalRequest>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken)
            => await _context.Set<ContentApprovalRequest>()
                .Where(x => x.BatchId == batchId)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ContentApprovalRequest>> GetPendingByTypeAsync(
            ImportEntityType entityType, CancellationToken cancellationToken)
            => await _context.Set<ContentApprovalRequest>()
                .Where(x => !x.IsDeleted && x.EntityType == entityType && x.Status == ContentApprovalStatus.PendingReview)
                .OrderBy(x => x.SubmittedAt)
                .ToListAsync(cancellationToken);
    }
}
