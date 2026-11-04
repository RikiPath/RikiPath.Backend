namespace RikiPath.Application.Responses.MentorScheduling;

// Thông tin số dư lượt học Mentor và các gói đang có hiệu lực của Học viên.
public sealed class LearnerCreditBalanceResponse
{
    // Tổng số lượt tư vấn khả dụng để đặt lịch.
    public int TotalAvailableCredits { get; set; }

    // Compatibility aliases for Mobile DTOs
    public int Balance => TotalAvailableCredits;
    public int AvailableBalance => TotalAvailableCredits;

    // Thời điểm hết hạn của gói sắp hết hạn nhất (UTC).
    public DateTime? ExpiringSoonestAtUtc { get; set; }
    public DateTime? ExpiresAtUtc => ExpiringSoonestAtUtc;

    // Danh sách các nguồn cấp tín dụng còn hạn và còn lượt.
    public List<CreditGrantItemResponse> ActiveGrants { get; set; } = new();

    // Lịch sử các biến động tín dụng gần nhất.
    public List<CreditEntryItemResponse> RecentEntries { get; set; } = new();
}

// Chi tiết một nguồn cấp tín dụng lượt học.
public sealed class CreditGrantItemResponse
{
    public long GrantId { get; set; }
    public int UserSubscriptionId { get; set; }
    public int? CertificateLevelId { get; set; }
    public string? JlptLevel { get; set; }
    public int PolicyVersion { get; set; }
    public int InitialQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// Chi tiết một bút toán biến động trong sổ cái tín dụng.
public sealed class CreditEntryItemResponse
{
    public long EntryId { get; set; }
    public long CreditGrantId { get; set; }
    public string EntryType { get; set; } = null!;
    public int Amount { get; set; }
    public string OperationKey { get; set; } = null!;
    public int? MentorBookingId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// Kết quả thực hiện trừ tín dụng khi đặt lịch.
public sealed class CreditSpendResult
{
    public bool IsSuccess { get; set; }
    public long CreditGrantId { get; set; }
    public long EntryId { get; set; }
    public int RemainingBalance { get; set; }
    public string OperationKey { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}

// Kết quả thực hiện hoàn lại tín dụng khi hủy lịch hợp lệ.
public sealed class CreditRefundResult
{
    public bool IsSuccess { get; set; }
    public long CreditGrantId { get; set; }
    public long EntryId { get; set; }
    public int RestoredBalance { get; set; }
    public string OperationKey { get; set; } = null!;
    public string? ErrorMessage { get; set; }
}

// Kết quả đối soát tính toàn vẹn của sổ cái tín dụng (Audit Reconciliation).
public sealed class CreditReconciliationResponse
{
    public int LearnerId { get; set; }
    public int TotalGrantsIssued { get; set; }
    public int TotalGrantsAvailable { get; set; }
    public int TotalEntriesNet { get; set; }
    public bool IsConsistent { get; set; }
    public int Discrepancy { get; set; }
}
