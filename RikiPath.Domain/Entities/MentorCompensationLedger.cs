using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

/// <summary>Quyết định duyệt điểm danh và thời lượng hợp lệ của buổi tư vấn phục vụ ghi nhận thù lao.</summary>
public sealed class MentorAttendanceApproval
{
    public long Id { get; set; }
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public MentorAttendanceApprovalStatus Status { get; set; } = MentorAttendanceApprovalStatus.PendingAdminReview;
    public int RawMentorDurationSeconds { get; set; }
    public int RawLearnerDurationSeconds { get; set; }
    public int? ApprovedEligibleMinutes { get; set; }
    public string EvidenceSummary { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public int? ReviewedByAdminId { get; set; }
    public UserAccount? ReviewedByAdmin { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public uint Version { get; set; }
}

/// <summary>Bút toán thù lao bất biến (append-only) của cố vấn; mỗi buổi tư vấn có tối đa một bút toán chính thức.</summary>
public sealed class MentorEarningEntry
{
    public long Id { get; set; }
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public long AttendanceApprovalId { get; set; }
    public MentorAttendanceApproval AttendanceApproval { get; set; } = null!;
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public int CertificateLevelId { get; set; }
    public CertificateLevel CertificateLevel { get; set; } = null!;
    public string JlptLevelCode { get; set; } = string.Empty;
    public decimal FrozenHourlyRate { get; set; }
    public decimal FrozenLevelCoefficient { get; set; }
    public int ApprovedEligibleMinutes { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int FormulaVersion { get; set; } = 1;
    public MentorEarningStatus Status { get; set; } = MentorEarningStatus.Eligible;
    public long? PayrollPeriodId { get; set; }
    public MentorPayrollPeriod? PayrollPeriod { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public int CreatedByAdminId { get; set; }
    public UserAccount CreatedByAdmin { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Bảng tổng hợp thù lao hằng tháng của cố vấn.</summary>
public sealed class MentorPayrollPeriod
{
    public long Id { get; set; }
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public int SequenceNumber { get; set; } = 1;
    public string? SupplementalReason { get; set; }
    public string Currency { get; set; } = "VND";
    public MentorPayrollStatus Status { get; set; } = MentorPayrollStatus.Draft;
    public decimal TotalEarningsAmount { get; set; }
    public decimal TotalAdjustmentsAmount { get; set; }
    public decimal GrossPayableAmount { get; set; }
    public int PreparedByAdminId { get; set; }
    public UserAccount PreparedByAdmin { get; set; } = null!;
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
    public int? ApprovedByAdminId { get; set; }
    public UserAccount? ApprovedByAdmin { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalNotes { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<MentorPayrollLine> Lines { get; set; } = new List<MentorPayrollLine>();
    public ICollection<MentorPayrollAdjustment> Adjustments { get; set; } = new List<MentorPayrollAdjustment>();
}

/// <summary>Dòng chi tiết bất biến thuộc kỳ bảng tổng hợp thù lao.</summary>
public sealed class MentorPayrollLine
{
    public long Id { get; set; }
    public long PayrollPeriodId { get; set; }
    public MentorPayrollPeriod PayrollPeriod { get; set; } = null!;
    public long EarningEntryId { get; set; }
    public MentorEarningEntry EarningEntry { get; set; } = null!;
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public decimal FrozenHourlyRate { get; set; }
    public decimal FrozenLevelCoefficient { get; set; }
    public int ApprovedEligibleMinutes { get; set; }
    public decimal LineAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Bút toán điều chỉnh có dấu (append-only) được quản trị viên phê duyệt.</summary>
public sealed class MentorPayrollAdjustment
{
    public long Id { get; set; }
    public long PayrollPeriodId { get; set; }
    public MentorPayrollPeriod PayrollPeriod { get; set; } = null!;
    public long? RelatedEarningEntryId { get; set; }
    public int? RelatedBookingId { get; set; }
    public decimal SignedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public int ApprovedByAdminId { get; set; }
    public UserAccount ApprovedByAdmin { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Tài khoản ngân hàng thụ hưởng của cố vấn có phân phiên bản và mã hóa số tài khoản.</summary>
public sealed class MentorBankAccount
{
    public long Id { get; set; }
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public int AccountVersion { get; set; } = 1;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string EncryptedAccountNumber { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountNumberHash { get; set; } = string.Empty;
    public MentorBankVerificationStatus VerificationStatus { get; set; } = MentorBankVerificationStatus.PendingVerification;
    public bool IsCurrent { get; set; } = true;
    public int? VerifiedByAdminId { get; set; }
    public UserAccount? VerifiedByAdmin { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerificationNotes { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public uint Version { get; set; }
}

/// <summary>Tài khoản kênh chi của tổ chức (chỉ lưu siêu dữ liệu đã che, không lưu bí mật hay mật khẩu ngân hàng).</summary>
public sealed class OrganizationPayoutAccount
{
    public long Id { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public PayoutExecutionMethod ProviderType { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string? ProviderAccountReference { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; } = true;
    public int ConfiguredByAdminId { get; set; }
    public UserAccount ConfiguredByAdmin { get; set; } = null!;
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Nghĩa vụ chi trả hợp nhất dùng chung cho cả chuyển khoản thủ công và lệnh chi payOS.</summary>
public sealed class MentorPayoutObligation
{
    public long Id { get; set; }
    public long PayrollPeriodId { get; set; }
    public MentorPayrollPeriod PayrollPeriod { get; set; } = null!;
    public int MentorUserId { get; set; }
    public UserAccount MentorUser { get; set; } = null!;
    public long? LockedBankAccountId { get; set; }
    public MentorBankAccount? LockedBankAccount { get; set; }
    public long? OrganizationPayoutAccountId { get; set; }
    public OrganizationPayoutAccount? OrganizationPayoutAccount { get; set; }
    public decimal PayableAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public PayoutExecutionMethod SelectedMethod { get; set; }
    public MentorPayoutObligationStatus Status { get; set; } = MentorPayoutObligationStatus.PendingExecution;
    public long? ActiveAttemptId { get; set; }
    public long? SettledAttemptId { get; set; }
    public string? SettledReference { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public int ApprovedByAdminId { get; set; }
    public UserAccount ApprovedByAdmin { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public uint Version { get; set; }
    public ICollection<MentorPayoutAttempt> Attempts { get; set; } = new List<MentorPayoutAttempt>();
}

/// <summary>Lần thực thi lệnh chi (append-only) thuộc nghĩa vụ chi trả hợp nhất.</summary>
public sealed class MentorPayoutAttempt
{
    public long Id { get; set; }
    public long PayoutObligationId { get; set; }
    public MentorPayoutObligation PayoutObligation { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public PayoutExecutionMethod ExecutionMethod { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public long? LockedBankAccountId { get; set; }
    public MentorBankAccount? LockedBankAccount { get; set; }
    public MentorPayoutAttemptStatus InternalStatus { get; set; } = MentorPayoutAttemptStatus.Initiated;
    public string? ProviderStatus { get; set; }
    public string? ProviderPayoutId { get; set; }
    public string? TransferReference { get; set; }
    public string? EvidenceStorageKey { get; set; }
    public string? EvidenceNotes { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int InitiatedByAdminId { get; set; }
    public UserAccount InitiatedByAdmin { get; set; } = null!;
    public int? ConfirmedByAdminId { get; set; }
    public UserAccount? ConfirmedByAdmin { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
