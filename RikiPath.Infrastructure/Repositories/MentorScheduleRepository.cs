using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    // // Triển khai repository quản lý mẫu lịch và quy tắc ca làm việc của Chuyên gia.
    public sealed class MentorScheduleRepository(AppDbContext context)
        : GenericRepository<MentorSchedule>(context), IMentorScheduleRepository
    {
        public Task<MentorSchedule?> GetByMentorIdWithRulesAsync(
            int mentorId, int policyVersion = 3, CancellationToken cancellationToken = default)
            => _context.MentorSchedules
                .Include(x => x.Mentor)
                .Include(x => x.Rules)
                .FirstOrDefaultAsync(x => x.MentorId == mentorId && x.PolicyVersion == policyVersion, cancellationToken);

        public Task<MentorSchedule?> GetByIdWithRulesAsync(long id, CancellationToken cancellationToken = default)
            => _context.MentorSchedules
                .Include(x => x.Mentor)
                .Include(x => x.Rules)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<MentorScheduleRule>> GetRulesByScheduleIdAsync(long scheduleId, CancellationToken cancellationToken = default)
            => _context.MentorScheduleRules
                .Where(x => x.ScheduleId == scheduleId)
                .OrderBy(x => x.Weekday)
                .ThenBy(x => x.LocalStart)
                .ToListAsync(cancellationToken);
    }
}
