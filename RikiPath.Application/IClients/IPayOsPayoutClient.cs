namespace RikiPath.Application.IClients;

public sealed record PayOsSinglePayoutCommand(
    string ReferenceId,
    long AmountVnd,
    string Description,
    string ToBin,
    string ToAccountNumber,
    IReadOnlyList<string>? Categories,
    string IdempotencyKey,
    string? QaSimulatedOutcome = null);

public sealed record PayOsBatchPayoutItemCommand(
    string ReferenceId,
    long AmountVnd,
    string Description,
    string ToBin,
    string ToAccountNumber);

public sealed record PayOsBatchPayoutCommand(
    string BatchReferenceId,
    bool ValidateDestination,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<PayOsBatchPayoutItemCommand> Payouts,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string>? QaSimulatedOutcomesByReferenceId = null);

public sealed record PayOsPayoutTransactionResult(
    string ReferenceId,
    string? ProviderPayoutId,
    string? ProviderBatchId,
    string? ProviderTransactionId,
    string ApprovalState,
    string TransactionState,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTimeoutOrUnknown,
    string RawPayloadSummaryJson);

public sealed record PayOsBatchPayoutResult(
    string BatchReferenceId,
    string? ProviderBatchId,
    IReadOnlyList<PayOsPayoutTransactionResult> Items,
    string RawPayloadSummaryJson);

public sealed record PayOsAccountBalanceResult(
    long BalanceVnd,
    string Currency,
    bool IsQaSimulated);

public sealed record PayOsCreditEstimateResult(
    long TotalAmountVnd,
    long EstimatedFeeVnd,
    long TotalCreditRequiredVnd,
    string Currency,
    bool IsQaSimulated);

public interface IPayOsPayoutClient
{
    Task<PayOsPayoutTransactionResult> CreateSinglePayoutAsync(
        PayOsSinglePayoutCommand command,
        CancellationToken cancellationToken = default);

    Task<PayOsBatchPayoutResult> CreateBatchPayoutAsync(
        PayOsBatchPayoutCommand command,
        CancellationToken cancellationToken = default);

    Task<PayOsPayoutTransactionResult> GetPayoutAsync(
        string providerPayoutId,
        CancellationToken cancellationToken = default);

    Task<PayOsPayoutTransactionResult?> FindPayoutByReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken = default);

    Task<PayOsCreditEstimateResult> EstimateCreditAsync(
        IReadOnlyList<PayOsBatchPayoutItemCommand> items,
        IReadOnlyList<string>? categories = null,
        CancellationToken cancellationToken = default);

    Task<PayOsAccountBalanceResult> GetAccountBalanceAsync(
        CancellationToken cancellationToken = default);
}
