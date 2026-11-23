using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface IUserSrsPreferenceRepository : IGenericRepository<UserSrsPreference>
{
    Task<UserSrsPreference?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<UserSrsPreference>> GetDueReminderCandidatesAsync(CancellationToken cancellationToken = default);
}
