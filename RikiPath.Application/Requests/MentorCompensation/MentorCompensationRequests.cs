using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.MentorCompensation;

public sealed class ConfigureMentorHourlyRateRequest
{
    public int MentorUserId { get; set; }
    public decimal HourlyRateVnd { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class ConfigureJlptCoefficientRequest
{
    public string JlptLevel { get; set; } = string.Empty;
    public decimal Coefficient { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class GrantMentorTeachingQualificationRequest
{
    public int MentorUserId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class RevokeMentorTeachingQualificationRequest
{
    public int MentorUserId { get; set; }
    public string JlptLevel { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class ReviewBookingAttendanceRequest
{
    public int ApprovedEligibleMinutes { get; set; } = 90;
    public bool Approve { get; set; } = true;
    public bool HoldDisputed { get; set; }
    public string? Reason { get; set; }
    public string? OperationKey { get; set; }
}

public sealed class GenerateMonthlyPayrollRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? MentorUserId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreatePayrollAdjustmentRequest
{
    public long AmountVnd { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? RelatedMentorBookingId { get; set; }
    public string? OperationKey { get; set; }
}

public sealed class ApproveMonthlyPayrollRequest
{
    public string? Notes { get; set; }
    public PayoutExecutionMethod? DefaultPayoutMethod { get; set; }
}

public sealed class UpsertMentorBankAccountRequest
{
    public string BankBin { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
}

public sealed class VerifyMentorBankAccountRequest
{
    public bool Approve { get; set; } = true;
    public string? VerificationMethod { get; set; }
    public string? Notes { get; set; }
}

public sealed class ConfigureOrganizationPayoutAccountRequest
{
    public long? Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public bool SupportsManualTransfer { get; set; } = true;
    public bool SupportsPayOsPayout { get; set; } = true;
    public string? PayOsPayoutChannelCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SelectPayoutMethodRequest
{
    public PayoutExecutionMethod ExecutionMethod { get; set; }
    public long? OrganizationPayoutAccountId { get; set; }
    public string? Notes { get; set; }
}

public sealed class InitiateManualPayoutRequest
{
    public long? OrganizationPayoutAccountId { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class ConfirmManualPayoutRequest
{
    public string TransferReference { get; set; } = string.Empty;
    public string EvidenceStorageKey { get; set; } = string.Empty;
    public DateTime? TransferredAtUtc { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class ExecutePayOsPayoutRequest
{
    public long? OrganizationPayoutAccountId { get; set; }
    public string? Description { get; set; }
    public List<string>? Categories { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? QaSimulatedOutcome { get; set; }
}

public sealed class ExecutePayOsBatchPayoutRequest
{
    public List<long> ObligationIds { get; set; } = [];
    public long? OrganizationPayoutAccountId { get; set; }
    public string? Description { get; set; }
    public List<string>? Categories { get; set; }
    public string? IdempotencyKey { get; set; }
    public Dictionary<long, string>? QaSimulatedOutcomesByObligation { get; set; }
}

public sealed class ReconcilePayoutObligationRequest
{
    public string? ProviderFinalState { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? Reason { get; set; }
}
