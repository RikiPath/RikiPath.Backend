using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

// Sổ cái nguồn cấp tín dụng lượt học Mentor gắn với gói học đã thanh toán.
public sealed class MentorCreditGrant
{
    public long Id { get; set; }

    // Học viên sở hữu lượt tư vấn.
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;

    // Nguồn gốc thanh toán gói học UserSubscription.
    public int UserSubscriptionId { get; set; }
    public UserSubscription UserSubscription { get; set; } = null!;

    // Phiên bản chính sách đặt lịch áp dụng.
    public int PolicyVersion { get; set; } = 3;

    // Tổng số lượt tư vấn ban đầu được cấp.
    public int InitialQuantity { get; set; }

    // Số lượt tư vấn hiện khả dụng để đặt lịch.
    public int AvailableQuantity { get; set; }

    // Thời điểm bắt đầu có hiệu lực (UTC).
    public DateTime EffectiveFromUtc { get; set; }

    // Thời điểm hết hạn sử dụng (UTC).
    public DateTime ExpiresAtUtc { get; set; }

    // Khóa phiên bản kiểm soát tương tranh lạc quan.
    public uint Version { get; set; }

    // Thời điểm tạo bản ghi nguồn cấp (UTC).
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Danh sách các bút toán ghi sổ chi tiết của nguồn cấp này.
    public ICollection<MentorCreditEntry> Entries { get; set; } = new List<MentorCreditEntry>();
}

// Bút toán chi tiết (append-only) trong sổ cái tín dụng phục vụ kiểm toán.
public sealed class MentorCreditEntry
{
    public long Id { get; set; }

    // Khóa ngoại liên kết với nguồn cấp tín dụng MentorCreditGrant.
    public long CreditGrantId { get; set; }
    public MentorCreditGrant CreditGrant { get; set; } = null!;

    // Học viên thực hiện giao dịch tín dụng.
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;

    // Phân loại bút toán giao dịch.
    public CreditEntryType EntryType { get; set; }

    // Số lượng thay đổi có dấu (+1 cho Grant/Refund, -1 cho Spend/Expire).
    public int Amount { get; set; }

    // Khóa định danh thao tác duy nhất để chống ghi trùng (Idempotency Key).
    public string OperationKey { get; set; } = null!;

    // Buổi đặt lịch liên quan nếu có (đối với Spend hoặc Refund).
    public int? MentorBookingId { get; set; }
    public MentorBooking? MentorBooking { get; set; }

    // Lý do hoặc ghi chú giải trình nghiệp vụ.
    public string? Reason { get; set; }

    // Thời điểm ghi nhận bút toán (UTC).
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
