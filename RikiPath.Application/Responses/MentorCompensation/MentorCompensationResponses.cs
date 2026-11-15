using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorCompensation;

public sealed class MentorHourlyRateResponse
{
    public int Id { get; set; }
    public int MentorUserId { get; set; }
    public string? MentorName { get; set; }
    public decimal HourlyRateVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class JlptLevelPayCoefficientResponse
{
    public int Id { get; set; }
    public int CertificateLevelId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public decimal Coefficient { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class MentorTeachingQualificationResponse
{
    public int Id { get; set; }
    public int MentorUserId { get; set; }
    public string? MentorName { get; set; }
    public int CertificateLevelId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class MentorAttendanceApprovalResponse
{
    public long Id { get; set; }
    public int MentorBookingId { get; set; }
    public int MentorUserId { get; set; }
    public int LearnerUserId { get; set; }
    public int ScheduledMinutes { get; set; }
    public int? MentorObservedSeconds { get; set; }
    public int? LearnerObservedSeconds { get; set; }
    public int ApprovedEligibleMinutes { get; set; }
    public MentorAttendanceApprovalStatus Status { get; set; }
    public string? DecisionReason { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public MentorEarningEntryResponse? EarningEntry { get; set; }
}

public sealed class MentorEarningEntryResponse
{
    public long Id { get; set; }
    public int MentorUserId { get; set; }
    public int MentorBookingId { get; set; }
    public long? AttendanceApprovalId { get; set; }
    public int CertificateLevelId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public long FrozenHourlyRateVnd { get; set; }
    public decimal FrozenLevelCoefficient { get; set; }
    public int ScheduledMinutes { get; set; }
    public int ApprovedEligibleMinutes { get; set; }
    public long ScheduledAmountVnd { get; set; }
    public long EligibleAmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public int FormulaVersion { get; set; }
    public MentorEarningStatus Status { get; set; }
    public long? MentorPayrollLineId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public DateTime EarnedAtUtc { get; set; }
}

public sealed class MentorPayrollLineResponse
{
    public long Id { get; set; }
    public long MentorPayrollPeriodId { get; set; }
    public long MentorEarningEntryId { get; set; }
    public int MentorBookingId { get; set; }
    public int CertificateLevelId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public long FrozenHourlyRateVnd { get; set; }
    public decimal FrozenLevelCoefficient { get; set; }
    public int ApprovedEligibleMinutes { get; set; }
    public long LineAmountVnd { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class MentorPayrollAdjustmentResponse
{
    public long Id { get; set; }
    public long MentorPayrollPeriodId { get; set; }
    public int MentorUserId { get; set; }
    public int? RelatedMentorBookingId { get; set; }
    public long AmountVnd { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class MentorPayrollPeriodResponse
{
    public long Id { get; set; }
    public int MentorUserId { get; set; }
    public string? MentorName { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public MentorPayrollStatus Status { get; set; }
    public long TotalEarningsAmountVnd { get; set; }
    public long TotalAdjustmentsAmountVnd { get; set; }
    public long GrossPayableAmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? Notes { get; set; }
    public long? PayoutObligationId { get; set; }
    public List<MentorPayrollLineResponse> Lines { get; set; } = [];
    public List<MentorPayrollAdjustmentResponse> Adjustments { get; set; } = [];
}

public sealed class MentorBankAccountResponse
{
    public long Id { get; set; }
    public int MentorUserId { get; set; }
    public string BankBin { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public MentorBankVerificationStatus VerificationStatus { get; set; }
    public string? VerificationMethod { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public int AccountVersion { get; set; }
    public bool IsCurrent { get; set; }
    public string? Notes { get; set; }
}

public sealed class OrganizationPayoutAccountResponse
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public bool SupportsManualTransfer { get; set; }
    public bool SupportsPayOsPayout { get; set; }
    public string? PayOsPayoutChannelCode { get; set; }
    public bool IsActive { get; set; }
}

public sealed class MentorPayoutAttemptResponse
{
    public long Id { get; set; }
    public long MentorPayoutObligationId { get; set; }
    public int AttemptNumber { get; set; }
    public PayoutExecutionMethod ExecutionMethod { get; set; }
    public MentorPayoutAttemptStatus Status { get; set; }
    public long AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestCorrelationId { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? ProviderPayoutId { get; set; }
    public string? ProviderBatchId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReferenceId { get; set; }
    public string? ProviderApprovalState { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? ProviderErrorMessage { get; set; }
    public string? TransferReference { get; set; }
    public string? EvidenceStorageKey { get; set; }
    public int InitiatedByUserId { get; set; }
    public DateTime InitiatedAtUtc { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class MentorPayoutObligationResponse
{
    public long Id { get; set; }
    public long MentorPayrollPeriodId { get; set; }
    public int MentorUserId { get; set; }
    public string? MentorName { get; set; }
    public long? MentorBankAccountId { get; set; }
    public long? OrganizationPayoutAccountId { get; set; }
    public PayoutExecutionMethod? SelectedExecutionMethod { get; set; }
    public MentorPayoutObligationStatus Status { get; set; }
    public long PayableAmountVnd { get; set; }
    public long PaidAmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string RecipientBankBinSnapshot { get; set; } = string.Empty;
    public string RecipientBankShortNameSnapshot { get; set; } = string.Empty;
    public string RecipientMaskedAccountSnapshot { get; set; } = string.Empty;
    public string RecipientAccountHolderSnapshot { get; set; } = string.Empty;
    public long? PaidByAttemptId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public int? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }
    public List<MentorPayoutAttemptResponse> Attempts { get; set; } = [];
}

public sealed class PayOsBatchPayoutExecutionResponse
{
    public string BatchReferenceId { get; set; } = string.Empty;
    public string? ProviderBatchId { get; set; }
    public int TotalObligations { get; set; }
    public int SucceededCount { get; set; }
    public int ProcessingCount { get; set; }
    public int FailedCount { get; set; }
    public int UnknownCount { get; set; }
    public List<MentorPayoutObligationResponse> Obligations { get; set; } = [];
}

public sealed class PayOsPayoutBalanceResponse
{
    public long BalanceVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsQaSimulated { get; set; }
}

public sealed class PayOsEstimateCreditResponse
{
    public long TotalAmountVnd { get; set; }
    public long EstimatedFeeVnd { get; set; }
    public long TotalCreditRequiredVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsQaSimulated { get; set; }
}

public sealed class MentorCompensationOverviewResponse
{
    public int MentorUserId { get; set; }
    public MentorHourlyRateResponse? CurrentHourlyRate { get; set; }
    public List<MentorTeachingQualificationResponse> ActiveQualifications { get; set; } = [];
    public MentorBankAccountResponse? CurrentBankAccount { get; set; }
    public long TotalEligibleUnassignedVnd { get; set; }
    public long TotalLockedInPayrollVnd { get; set; }
    public long TotalPaidVnd { get; set; }
    public List<MentorEarningEntryResponse> RecentEarnings { get; set; } = [];
    public List<MentorPayrollPeriodResponse> RecentPayrolls { get; set; } = [];
    public List<MentorPayoutObligationResponse> RecentPayouts { get; set; } = [];
}
