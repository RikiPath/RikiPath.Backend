using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

// Repository quản lý truy vấn và ghi sổ cái tín dụng MentorCreditLedger.
public interface IMentorCreditLedgerRepository
{
    // Lấy danh sách các nguồn cấp tín dụng còn hạn và còn số dư theo thứ tự ưu tiên FIFO (hết hạn sớm dùng trước).
    Task<List<MentorCreditGrant>> GetActiveGrantsForLearnerAsync(int learnerId, DateTime asOfUtc, CancellationToken cancellationToken = default);

    // Lấy chi tiết nguồn cấp tín dụng theo ID.
    Task<MentorCreditGrant?> GetGrantByIdAsync(long grantId, CancellationToken cancellationToken = default);

    // Lấy nguồn cấp tín dụng gắn với gói học đã mua (UserSubscription).
    Task<MentorCreditGrant?> GetGrantBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);

    // Tìm bút toán biến động theo khóa thao tác duy nhất (OperationKey / Idempotency Key).
    Task<MentorCreditEntry?> GetEntryByOperationKeyAsync(string operationKey, CancellationToken cancellationToken = default);

    // Lấy các bút toán liên quan đến một buổi hẹn đặt lịch.
    Task<List<MentorCreditEntry>> GetEntriesByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default);

    // Lấy lịch sử bút toán của một học viên có phân trang.
    Task<List<MentorCreditEntry>> GetEntriesByLearnerIdAsync(int learnerId, int skip, int take, CancellationToken cancellationToken = default);

    // Lấy toàn bộ các nguồn cấp tín dụng của một học viên phục vụ đối soát.
    Task<List<MentorCreditGrant>> GetAllGrantsForLearnerAsync(int learnerId, CancellationToken cancellationToken = default);

    // Thêm mới một nguồn cấp tín dụng.
    Task AddGrantAsync(MentorCreditGrant grant, CancellationToken cancellationToken = default);

    // Cập nhật số dư nguồn cấp tín dụng.
    void UpdateGrant(MentorCreditGrant grant);

    // Thêm mới một bút toán vào sổ cái tín dụng (append-only).
    Task AddEntryAsync(MentorCreditEntry entry, CancellationToken cancellationToken = default);
}
