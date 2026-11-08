using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;

namespace RikiPath.Application.IServices;

// Giao diện dịch vụ quản lý sổ cái tín dụng lượt học Mentor (Credit Ledger).
public interface IMentorCreditLedgerService
{
    // Học viên xem số dư lượt tư vấn khả dụng, thời hạn gói và lịch sử biến động.
    Task<ApiResponse<LearnerCreditBalanceResponse>> GetMyCreditBalanceAsync(CancellationToken cancellationToken = default);

    // Học viên xem chi tiết lịch sử bút toán sổ cái có phân trang.
    Task<ApiResponse<List<CreditEntryItemResponse>>> GetMyCreditHistoryAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    // Cấp lượt tư vấn mới từ gói học đã thanh toán thành công (Paid) theo nguyên tắc bất biến và idempotent.
    Task<ApiResponse<CreditGrantItemResponse>> CreateGrantFromPaidSubscriptionAsync(int subscriptionId, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    // Trừ 1 lượt tư vấn theo nguyên tắc FIFO khi đặt lịch thành công (tham gia vào atomic booking transaction).
    Task<CreditSpendResult> SpendCreditAsync(int learnerId, int bookingId, string operationKey, DateTime appointmentTimeUtc, CancellationToken cancellationToken = default);

    // Hoàn lại 1 lượt tư vấn về đúng nguồn cấp ban đầu khi buổi hẹn bị hủy hợp lệ.
    Task<CreditRefundResult> RefundCreditAsync(int learnerId, int bookingId, string operationKey, string reason, CancellationToken cancellationToken = default);

    // Quản trị viên đối soát tính toàn vẹn và khớp đúng giữa sổ cái bút toán và số dư nguồn cấp của học viên.
    Task<ApiResponse<CreditReconciliationResponse>> ReconcileLearnerCreditsForAdminAsync(int learnerId, CancellationToken cancellationToken = default);
}
