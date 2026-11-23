using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public class UserSrsPreferenceRepository(AppDbContext context)
    : GenericRepository<UserSrsPreference>(context), IUserSrsPreferenceRepository
{
    public async Task<UserSrsPreference?> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => await _context.UserSrsPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);

    public async Task<List<UserSrsPreference>> GetDueReminderCandidatesAsync(
        CancellationToken cancellationToken = default)
        => await _context.UserSrsPreferences
            .Include(x => x.UserAccount)
            .Where(x => !x.IsDeleted
                && x.DailyReminderEnabled
                && !x.UserAccount.IsDeleted)
            .ToListAsync(cancellationToken);
}
