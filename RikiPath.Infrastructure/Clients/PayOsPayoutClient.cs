using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients;

// Adapter tích hợp payOS Payout API (/v1/payouts, /v1/payouts/batch, /v1/payouts/{id}, /v1/payouts/estimate-credit, /v1/payouts-account/balance)
// kèm bộ mô phỏng QA tất định và khóa an toàn mặc định LivePayoutEnabled = false.
public sealed class PayOsPayoutClient : IPayOsPayoutClient
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _appSettings;
    private readonly ILogger<PayOsPayoutClient> _logger;

    // Lưu trạng thái giao dịch mô phỏng QA theo ReferenceId và ProviderPayoutId để phục vụ đối soát (reconciliation)
    private static readonly ConcurrentDictionary<string, PayOsPayoutTransactionResult> QaSimulatedByReference = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, PayOsPayoutTransactionResult> QaSimulatedByProviderId = new(StringComparer.OrdinalIgnoreCase);

    public PayOsPayoutClient(
        HttpClient httpClient,
        AppSettings appSettings,
        ILogger<PayOsPayoutClient> logger)
    {
        _httpClient = httpClient;
        _appSettings = appSettings;
        _logger = logger;
    }

    public static string ComputeSignature(IReadOnlyDictionary<string, string> sortedFields, string checksumKey)
    {
        if (string.IsNullOrWhiteSpace(checksumKey))
            throw new InvalidOperationException("Thiếu ChecksumKey để ký yêu cầu payOS Payout.");

        var canonical = string.Join("&", sortedFields
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool VerifySignature(IReadOnlyDictionary<string, string> sortedFields, string checksumKey, string expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(expectedSignature))
            return false;

        var computed = ComputeSignature(sortedFields, checksumKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(expectedSignature.Trim().ToLowerInvariant()));
    }

    public async Task<PayOsPayoutTransactionResult> CreateSinglePayoutAsync(
        PayOsSinglePayoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var settings = _appSettings.PayOsPayout ?? new PayOsPayoutSettings();
        var checksumKey = string.IsNullOrWhiteSpace(settings.ChecksumKey) ? "rikipath-qa-payout-checksum-key" : settings.ChecksumKey;

        var signFields = new Dictionary<string, string>
        {
            ["amount"] = command.AmountVnd.ToString(),
            ["description"] = command.Description,
            ["referenceId"] = command.ReferenceId,
            ["toAccountNumber"] = command.ToAccountNumber,
            ["toBin"] = command.ToBin
        };
        var signature = ComputeSignature(signFields, checksumKey);

        if (settings.QaSimulatorEnabled || !string.IsNullOrWhiteSpace(command.QaSimulatedOutcome))
        {
            return SimulateSinglePayout(command, signature);
        }

        if (!settings.LivePayoutEnabled)
        {
            throw new InvalidOperationException("Chi trả payOS Payout trực tiếp (LivePayoutEnabled) đang bị khóa theo chính sách an toàn mặc định.");
        }

        EnsureLiveCredentials(settings);

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/payouts";
        var payload = new
        {
            referenceId = command.ReferenceId,
            amount = command.AmountVnd,
            description = command.Description,
            toBin = command.ToBin,
            toAccountNumber = command.ToAccountNumber,
            category = command.Categories ?? ["salary"]
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        AddPayOsHeaders(req, settings, command.IdempotencyKey, signature);

        try
        {
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            return ParsePayOsPayoutResponse(command.ReferenceId, (int)resp.StatusCode, body);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or TimeoutException)
        {
            _logger.LogWarning(ex, "payOS Payout timeout/network error for reference {ReferenceId}; marking UnknownPendingReconciliation.", command.ReferenceId);
            return new PayOsPayoutTransactionResult(
                ReferenceId: command.ReferenceId,
                ProviderPayoutId: null,
                ProviderBatchId: null,
                ProviderTransactionId: null,
                ApprovalState: "UNKNOWN",
                TransactionState: "UNKNOWN",
                ErrorCode: "NETWORK_TIMEOUT",
                ErrorMessage: "Không xác định được phản hồi từ payOS Payout; cần đối soát trước khi thử lại.",
                IsTimeoutOrUnknown: true,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId = command.ReferenceId, error = "TIMEOUT_OR_NETWORK_ERROR" }));
        }
    }

    public async Task<PayOsBatchPayoutResult> CreateBatchPayoutAsync(
        PayOsBatchPayoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var settings = _appSettings.PayOsPayout ?? new PayOsPayoutSettings();
        var checksumKey = string.IsNullOrWhiteSpace(settings.ChecksumKey) ? "rikipath-qa-payout-checksum-key" : settings.ChecksumKey;

        var payoutsJson = JsonSerializer.Serialize(command.Payouts.Select(p => new
        {
            referenceId = p.ReferenceId,
            amount = p.AmountVnd,
            description = p.Description,
            toBin = p.ToBin,
            toAccountNumber = p.ToAccountNumber
        }));

        var signFields = new Dictionary<string, string>
        {
            ["payouts"] = payoutsJson,
            ["referenceId"] = command.BatchReferenceId,
            ["validateDestination"] = command.ValidateDestination ? "true" : "false"
        };
        var signature = ComputeSignature(signFields, checksumKey);

        if (settings.QaSimulatorEnabled || (command.QaSimulatedOutcomesByReferenceId != null && command.QaSimulatedOutcomesByReferenceId.Count > 0))
        {
            return SimulateBatchPayout(command, signature);
        }

        if (!settings.LivePayoutEnabled)
        {
            throw new InvalidOperationException("Chi trả hàng loạt payOS Payout trực tiếp (LivePayoutEnabled) đang bị khóa theo chính sách an toàn mặc định.");
        }

        EnsureLiveCredentials(settings);

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/payouts/batch";
        var payload = new
        {
            referenceId = command.BatchReferenceId,
            validateDestination = command.ValidateDestination,
            category = command.Categories ?? ["salary"],
            payouts = command.Payouts.Select(p => new
            {
                referenceId = p.ReferenceId,
                amount = p.AmountVnd,
                description = p.Description,
                toBin = p.ToBin,
                toAccountNumber = p.ToAccountNumber
            }).ToList()
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        AddPayOsHeaders(req, settings, command.IdempotencyKey, signature);

        try
        {
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            var single = ParsePayOsPayoutResponse(command.BatchReferenceId, (int)resp.StatusCode, body);
            var items = command.Payouts.Select(p => single with { ReferenceId = p.ReferenceId }).ToList();
            return new PayOsBatchPayoutResult(command.BatchReferenceId, single.ProviderBatchId ?? single.ProviderPayoutId, items, single.RawPayloadSummaryJson);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or TimeoutException)
        {
            _logger.LogWarning(ex, "payOS Batch Payout timeout/network error for batch {BatchReferenceId}.", command.BatchReferenceId);
            var items = command.Payouts.Select(p => new PayOsPayoutTransactionResult(
                ReferenceId: p.ReferenceId,
                ProviderPayoutId: null,
                ProviderBatchId: null,
                ProviderTransactionId: null,
                ApprovalState: "UNKNOWN",
                TransactionState: "UNKNOWN",
                ErrorCode: "NETWORK_TIMEOUT",
                ErrorMessage: "Không xác định được phản hồi từ payOS Batch Payout; cần đối soát trước khi thử lại.",
                IsTimeoutOrUnknown: true,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId = p.ReferenceId, error = "TIMEOUT_OR_NETWORK_ERROR" }))).ToList();

            return new PayOsBatchPayoutResult(command.BatchReferenceId, null, items, "{\"error\":\"TIMEOUT_OR_NETWORK_ERROR\"}");
        }
    }

    public Task<PayOsPayoutTransactionResult> GetPayoutAsync(
        string providerPayoutId,
        CancellationToken cancellationToken = default)
    {
        if (QaSimulatedByProviderId.TryGetValue(providerPayoutId, out var simulated))
        {
            return Task.FromResult(simulated);
        }

        return Task.FromResult(new PayOsPayoutTransactionResult(
            ReferenceId: string.Empty,
            ProviderPayoutId: providerPayoutId,
            ProviderBatchId: null,
            ProviderTransactionId: null,
            ApprovalState: "NOT_FOUND",
            TransactionState: "NOT_FOUND",
            ErrorCode: "404",
            ErrorMessage: "Không tìm thấy giao dịch trên payOS.",
            IsTimeoutOrUnknown: false,
            RawPayloadSummaryJson: JsonSerializer.Serialize(new { providerPayoutId, state = "NOT_FOUND" })));
    }

    public Task<PayOsPayoutTransactionResult?> FindPayoutByReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        if (QaSimulatedByReference.TryGetValue(referenceId, out var simulated))
        {
            return Task.FromResult<PayOsPayoutTransactionResult?>(simulated);
        }

        return Task.FromResult<PayOsPayoutTransactionResult?>(null);
    }

    public Task<PayOsCreditEstimateResult> EstimateCreditAsync(
        IReadOnlyList<PayOsBatchPayoutItemCommand> items,
        IReadOnlyList<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var totalAmount = items.Sum(x => x.AmountVnd);
        var estimatedFee = items.Count * 1100L;
        return Task.FromResult(new PayOsCreditEstimateResult(
            TotalAmountVnd: totalAmount,
            EstimatedFeeVnd: estimatedFee,
            TotalCreditRequiredVnd: totalAmount + estimatedFee,
            Currency: "VND",
            IsQaSimulated: true));
    }

    public Task<PayOsAccountBalanceResult> GetAccountBalanceAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PayOsAccountBalanceResult(
            BalanceVnd: 500_000_000L,
            Currency: "VND",
            IsQaSimulated: true));
    }

    private static PayOsPayoutTransactionResult SimulateSinglePayout(PayOsSinglePayoutCommand command, string signature)
    {
        var outcome = (command.QaSimulatedOutcome ?? "COMPLETED").Trim().ToUpperInvariant();
        var providerPayoutId = $"payos_qa_{command.ReferenceId}";
        var maskedDest = command.ToAccountNumber.Length > 4
            ? $"****{command.ToAccountNumber[^4..]}"
            : "****";

        PayOsPayoutTransactionResult result = outcome switch
        {
            "TIMEOUT" or "UNKNOWN" => new PayOsPayoutTransactionResult(
                ReferenceId: command.ReferenceId,
                ProviderPayoutId: null,
                ProviderBatchId: null,
                ProviderTransactionId: null,
                ApprovalState: "UNKNOWN",
                TransactionState: "UNKNOWN",
                ErrorCode: "QA_SIMULATED_TIMEOUT",
                ErrorMessage: "Mô phỏng mất kết nối / timeout khi gọi payOS Payout; cần đối soát trước khi thử lại.",
                IsTimeoutOrUnknown: true,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new
                {
                    code = "TIMEOUT",
                    referenceId = command.ReferenceId,
                    toBin = command.ToBin,
                    toAccountMasked = maskedDest,
                    signaturePrefix = signature[..12],
                    isQaSimulated = true
                })),

            "PROCESSING" => new PayOsPayoutTransactionResult(
                ReferenceId: command.ReferenceId,
                ProviderPayoutId: providerPayoutId,
                ProviderBatchId: null,
                ProviderTransactionId: $"txn_proc_{command.ReferenceId}",
                ApprovalState: "PROCESSING",
                TransactionState: "PROCESSING",
                ErrorCode: "00",
                ErrorMessage: null,
                IsTimeoutOrUnknown: false,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new
                {
                    code = "00",
                    desc = "Success (Processing)",
                    approvalState = "PROCESSING",
                    referenceId = command.ReferenceId,
                    providerPayoutId,
                    toBin = command.ToBin,
                    toAccountMasked = maskedDest,
                    signaturePrefix = signature[..12],
                    isQaSimulated = true
                })),

            "FAILED" => new PayOsPayoutTransactionResult(
                ReferenceId: command.ReferenceId,
                ProviderPayoutId: providerPayoutId,
                ProviderBatchId: null,
                ProviderTransactionId: $"txn_fail_{command.ReferenceId}",
                ApprovalState: "FAILED",
                TransactionState: "FAILED",
                ErrorCode: "PAYOUT_REJECTED",
                ErrorMessage: "Giao dịch chi trả bị ngân hàng/nhà cung cấp từ chối (QA Simulated).",
                IsTimeoutOrUnknown: false,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new
                {
                    code = "PAYOUT_REJECTED",
                    approvalState = "FAILED",
                    referenceId = command.ReferenceId,
                    providerPayoutId,
                    toBin = command.ToBin,
                    toAccountMasked = maskedDest,
                    signaturePrefix = signature[..12],
                    isQaSimulated = true
                })),

            _ => new PayOsPayoutTransactionResult(
                ReferenceId: command.ReferenceId,
                ProviderPayoutId: providerPayoutId,
                ProviderBatchId: null,
                ProviderTransactionId: $"ft_qa_{command.ReferenceId}",
                ApprovalState: "COMPLETED",
                TransactionState: "SUCCEEDED",
                ErrorCode: "00",
                ErrorMessage: null,
                IsTimeoutOrUnknown: false,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new
                {
                    code = "00",
                    desc = "Success",
                    approvalState = "COMPLETED",
                    transactionState = "SUCCEEDED",
                    referenceId = command.ReferenceId,
                    providerPayoutId,
                    toBin = command.ToBin,
                    toAccountMasked = maskedDest,
                    signaturePrefix = signature[..12],
                    isQaSimulated = true
                }))
        };

        QaSimulatedByReference[command.ReferenceId] = result;
        if (!string.IsNullOrWhiteSpace(result.ProviderPayoutId))
        {
            QaSimulatedByProviderId[result.ProviderPayoutId] = result;
        }

        return result;
    }

    private static PayOsBatchPayoutResult SimulateBatchPayout(PayOsBatchPayoutCommand command, string signature)
    {
        var providerBatchId = $"payos_batch_qa_{command.BatchReferenceId}";
        var items = new List<PayOsPayoutTransactionResult>(command.Payouts.Count);

        foreach (var item in command.Payouts)
        {
            string? outcome = null;
            if (command.QaSimulatedOutcomesByReferenceId != null)
            {
                command.QaSimulatedOutcomesByReferenceId.TryGetValue(item.ReferenceId, out outcome);
            }

            var singleCmd = new PayOsSinglePayoutCommand(
                ReferenceId: item.ReferenceId,
                AmountVnd: item.AmountVnd,
                Description: item.Description,
                ToBin: item.ToBin,
                ToAccountNumber: item.ToAccountNumber,
                Categories: command.Categories,
                IdempotencyKey: $"{command.IdempotencyKey}:{item.ReferenceId}",
                QaSimulatedOutcome: outcome ?? "COMPLETED");

            var simulatedItem = SimulateSinglePayout(singleCmd, signature) with
            {
                ProviderBatchId = providerBatchId
            };
            QaSimulatedByReference[item.ReferenceId] = simulatedItem;
            items.Add(simulatedItem);
        }

        var summary = JsonSerializer.Serialize(new
        {
            batchReferenceId = command.BatchReferenceId,
            providerBatchId,
            totalItems = items.Count,
            signaturePrefix = signature[..12],
            isQaSimulated = true
        });

        return new PayOsBatchPayoutResult(command.BatchReferenceId, providerBatchId, items, summary);
    }

    private static void EnsureLiveCredentials(PayOsPayoutSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.ChecksumKey))
        {
            throw new InvalidOperationException("Thiếu thông tin xác thực payOS Payout (ClientId, ApiKey, ChecksumKey).");
        }
    }

    private static void AddPayOsHeaders(
        HttpRequestMessage req,
        PayOsPayoutSettings settings,
        string idempotencyKey,
        string signature)
    {
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Add("x-client-id", settings.ClientId);
        req.Headers.Add("x-api-key", settings.ApiKey);
        req.Headers.Add("x-idempotency-key", idempotencyKey);
        req.Headers.Add("x-signature", signature);
    }

    private static PayOsPayoutTransactionResult ParsePayOsPayoutResponse(string referenceId, int statusCode, string body)
    {
        if (statusCode >= 500 || statusCode == 408 || statusCode == 429)
        {
            return new PayOsPayoutTransactionResult(
                ReferenceId: referenceId,
                ProviderPayoutId: null,
                ProviderBatchId: null,
                ProviderTransactionId: null,
                ApprovalState: "UNKNOWN",
                TransactionState: "UNKNOWN",
                ErrorCode: $"HTTP_{statusCode}",
                ErrorMessage: "Phản hồi HTTP không xác định từ payOS Payout; cần đối soát.",
                IsTimeoutOrUnknown: true,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId, statusCode }));
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var desc = root.TryGetProperty("desc", out var d) ? d.GetString() : null;

            if (code != "00" || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return new PayOsPayoutTransactionResult(
                    ReferenceId: referenceId,
                    ProviderPayoutId: null,
                    ProviderBatchId: null,
                    ProviderTransactionId: null,
                    ApprovalState: "FAILED",
                    TransactionState: "FAILED",
                    ErrorCode: code ?? $"HTTP_{statusCode}",
                    ErrorMessage: desc ?? "Giao dịch chi trả payOS thất bại.",
                    IsTimeoutOrUnknown: false,
                    RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId, code, desc }));
            }

            var id = data.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var approvalState = data.TryGetProperty("approvalState", out var appProp) ? appProp.GetString() ?? "PROCESSING" : "PROCESSING";
            var txState = approvalState;
            string? txId = null;

            if (data.TryGetProperty("transactions", out var txs) && txs.ValueKind == JsonValueKind.Array && txs.GetArrayLength() > 0)
            {
                var firstTx = txs[0];
                if (firstTx.TryGetProperty("id", out var tId)) txId = tId.GetString();
                if (firstTx.TryGetProperty("state", out var tState)) txState = tState.GetString() ?? approvalState;
            }

            return new PayOsPayoutTransactionResult(
                ReferenceId: referenceId,
                ProviderPayoutId: id,
                ProviderBatchId: null,
                ProviderTransactionId: txId,
                ApprovalState: approvalState,
                TransactionState: txState,
                ErrorCode: "00",
                ErrorMessage: null,
                IsTimeoutOrUnknown: false,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId, id, approvalState, txState }));
        }
        catch
        {
            return new PayOsPayoutTransactionResult(
                ReferenceId: referenceId,
                ProviderPayoutId: null,
                ProviderBatchId: null,
                ProviderTransactionId: null,
                ApprovalState: "UNKNOWN",
                TransactionState: "UNKNOWN",
                ErrorCode: "PARSE_ERROR",
                ErrorMessage: "Không thể phân tích phản hồi từ payOS Payout; cần đối soát.",
                IsTimeoutOrUnknown: true,
                RawPayloadSummaryJson: JsonSerializer.Serialize(new { referenceId, statusCode, parseError = true }));
        }
    }
}
