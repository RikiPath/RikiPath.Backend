using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public sealed class StudyCourseRepository(AppDbContext context) : GenericRepository<StudyCourse>(context), IStudyCourseRepository
{
    public Task<StudyCourse?> LockAsync(int id, CancellationToken ct) => context.Set<StudyCourse>()
        .FromSqlInterpolated($"SELECT * FROM \"StudyCourses\" WHERE \"Id\" = {id} FOR UPDATE")
        .SingleOrDefaultAsync(ct);

    public async Task LockPurchaseAsync(int learnerId, Guid key, CancellationToken ct)
        => await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"course:{learnerId}:{key}"}, 0))", ct);

    public async Task LockScheduleAsync(int mentorId, int learnerId, CancellationToken ct)
    {
        foreach (var key in new[] { $"course-schedule:{mentorId}", $"course-schedule:{learnerId}" }.OrderBy(x => x))
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
}
