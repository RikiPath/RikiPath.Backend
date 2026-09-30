using RikiPath.Domain.Enums;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IUserAccountRepository : IGenericRepository<UserAccount>
    {
        Task<UserAccount?> GetByEmailAsync(string email);
        Task<List<UserAccount>> GetByRoleAsync(Role role, int pageIndex, int pageSize);
        Task<int> CountByRoleAsync(Role role);
    }
}
