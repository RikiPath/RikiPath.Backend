using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IRepositories
{

    // Repository quản lý truy vấn và thao tác trên bảng MentorProfiles.
      public interface IMentorProfileRepository : IGenericRepository<MentorProfile>
    {
        // Lấy hồ sơ chuyên gia theo MentorId.
        Task<MentorProfile?> GetByMentorIdAsync(int mentorId, CancellationToken cancellationToken = default);

        // Lấy hồ sơ chuyên gia kèm thông tin tài khoản người dùng UserAccount.
        Task<MentorProfile?> GetWithMentorAsync(int mentorId, CancellationToken cancellationToken = default);

        // Lấy danh sách các hồ sơ chuyên gia đang chờ Quản trị viên xét duyệt (Pending).
        Task<IReadOnlyList<MentorProfile>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default);

        // Lấy danh sách hồ sơ chuyên gia theo trạng thái xác minh cụ thể.
        Task<IReadOnlyList<MentorProfile>> GetByStatusAsync(MentorVerificationStatus status, CancellationToken cancellationToken = default);

        // Tìm kiếm và phân trang danh sách chuyên gia đã được phê duyệt công khai.
        Task<(IReadOnlyList<UserAccount> Mentors, int TotalCount)> SearchApprovedMentorsAsync(
            string? searchTerm, string? expertise, string? jlptLevel, int page, int pageSize, string? sortBy, CancellationToken cancellationToken = default);
    }
}
