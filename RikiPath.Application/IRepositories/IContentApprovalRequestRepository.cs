using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories
{
    public interface IContentApprovalRequestRepository : IGenericRepository<ContentApprovalRequest>
    {
        Task<ContentApprovalRequest?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken);

        Task<IReadOnlyList<ContentApprovalRequest>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken);

        Task<IReadOnlyList<ContentApprovalRequest>> GetPendingByTypeAsync(
            ImportEntityType entityType, CancellationToken cancellationToken);
    }
}
