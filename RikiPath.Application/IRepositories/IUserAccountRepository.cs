using RikiPath.Domain.Enums;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IUserAccountRepository : IGenericRepository<UserAccount>
    {
        Task<UserAccount?> GetByEmailAsync(string email);
        Task<UserAccount?> GetWithLearningGoalAsync(int userId, CancellationToken cancellationToken = default);
        Task<UserAccount?> GetByIdForUpdateAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<UserAccount>> GetByRoleAsync(Role role, int pageIndex, int pageSize);
        Task<int> CountByRoleAsync(Role role);
    }
}
