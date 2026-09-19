using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class AiCreditTopUpRepository(AppDbContext context) : GenericRepository<AiCreditTopUp>(context), IAiCreditTopUpRepository
    {
        public async Task<IReadOnlyList<AiCreditTopUp>> GetTopUpHistoryByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await _dbSet.Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedDate).ToListAsync(ct);
        }
    }
}
