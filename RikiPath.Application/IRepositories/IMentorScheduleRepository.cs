using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    // // Repository quản lý truy vấn và thao tác trên bảng MentorSchedules và MentorScheduleRules.
    public interface IMentorScheduleRepository : IGenericRepository<MentorSchedule>
    {
        // // Lấy mẫu lịch của Chuyên gia kèm theo danh sách các quy tắc lặp.
        Task<MentorSchedule?> GetByMentorIdWithRulesAsync(int mentorId, int policyVersion = 3, CancellationToken cancellationToken = default);

        // // Lấy mẫu lịch theo ID kèm theo danh sách quy tắc.
        Task<MentorSchedule?> GetByIdWithRulesAsync(long id, CancellationToken cancellationToken = default);

        // // Lấy danh sách quy tắc ca làm việc của một mẫu lịch.
        Task<List<MentorScheduleRule>> GetRulesByScheduleIdAsync(long scheduleId, CancellationToken cancellationToken = default);
    }
}
