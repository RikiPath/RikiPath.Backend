using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface IStudyCourseRepository : IGenericRepository<StudyCourse>
{
    Task<StudyCourse?> LockAsync(int id, CancellationToken ct);
    Task LockPurchaseAsync(int learnerId, Guid key, CancellationToken ct);
    Task LockScheduleAsync(int mentorId, int learnerId, CancellationToken ct);
}
