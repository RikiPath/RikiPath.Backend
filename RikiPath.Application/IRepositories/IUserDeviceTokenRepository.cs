using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IUserDeviceTokenRepository : IGenericRepository<UserDeviceToken>
    {
        Task<List<UserDeviceToken>> GetActiveTokensByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<UserDeviceToken>> GetActiveTokensByUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);
        Task<UserDeviceToken?> GetByTokenAsync(string fcmToken, CancellationToken cancellationToken = default);
        Task<UserDeviceToken?> GetByUserAndDeviceAsync(int userId, string deviceIdentifier, CancellationToken cancellationToken = default);
        Task DeactivateTokenAsync(string fcmToken, CancellationToken cancellationToken = default);
        Task DeactivateDeviceAsync(int userId, string deviceIdentifier, CancellationToken cancellationToken = default);
        Task<UserDeviceToken> RegisterOrUpdateTokenAsync(int userId, string deviceIdentifier, string platform, string fcmToken, CancellationToken cancellationToken = default);
    }
}
