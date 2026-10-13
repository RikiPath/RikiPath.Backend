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

        public async Task<IReadOnlyList<ContentApprovalRequest>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken)
            => await _context.Set<ContentApprovalRequest>()
                .Where(x => x.BatchId == batchId)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ContentApprovalRequest>> GetPendingByTypeAsync(
            ImportEntityType entityType, CancellationToken cancellationToken)
            => await _context.Set<ContentApprovalRequest>()
                .Where(x => x.EntityType == entityType &&
                            (x.Status == ContentApprovalStatus.Draft || x.Status == ContentApprovalStatus.PendingReview))
                .OrderBy(x => x.SubmittedAt)
                .ToListAsync(cancellationToken);
    }
}
