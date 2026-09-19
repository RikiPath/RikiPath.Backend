using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IAiCreditTopUpRepository : IGenericRepository<AiCreditTopUp>
    {
        Task<IReadOnlyList<AiCreditTopUp>> GetTopUpHistoryByUserIdAsync(int userId, CancellationToken ct = default);
    }
}
