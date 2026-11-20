using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public sealed class StudyCourseRepository(AppDbContext context) : GenericRepository<StudyCourse>(context), IStudyCourseRepository
{
    public Task<StudyCourse?> LockAsync(int id, CancellationToken ct) => context.Database.IsRelational()
        ? context.Set<StudyCourse>()
            .FromSqlInterpolated($"SELECT * FROM \"StudyCourses\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
        : context.Set<StudyCourse>().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task LockCohortAsync(int id, CancellationToken ct)
    {
        if (!context.Database.IsRelational()) return;
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({$"study-cohort:{id}"}, 0))", ct);
    }

    public async Task LockPurchaseAsync(int learnerId, Guid key, CancellationToken ct)
    {
        if (!context.Database.IsRelational()) return;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"course:{learnerId}:{key}"}, 0))", ct);
    }

    public async Task LockScheduleAsync(int mentorId, int learnerId, CancellationToken ct)
    {
        // PostgreSQL dùng advisory lock; provider InMemory trong unit test không hỗ trợ SQL.
        if (!context.Database.IsRelational()) return;
        foreach (var key in new[] { $"course-schedule:{mentorId}", $"course-schedule:{learnerId}" }.OrderBy(x => x))
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
}
