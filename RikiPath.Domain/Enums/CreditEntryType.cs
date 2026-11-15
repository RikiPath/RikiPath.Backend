namespace RikiPath.Domain.Enums;

// Loại giao dịch trong sổ cái tín dụng (Credit Ledger).
public enum CreditEntryType
{
    Grant = 1,       // Cấp lượt tư vấn mới từ gói học đã thanh toán
    Spend = 2,       // Trừ lượt tư vấn khi đặt lịch thành công
    Refund = 3,      // Hoàn lại lượt tư vấn khi hủy lịch hợp lệ
    Expire = 4,      // Hết hạn thời gian sử dụng lượt tư vấn
    Adjustment = 5   // Điều chỉnh hạn mức bởi Quản trị viên
}
