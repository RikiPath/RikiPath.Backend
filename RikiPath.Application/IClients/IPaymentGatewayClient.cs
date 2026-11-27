using RikiPath.Application.Requests.Payments;

namespace RikiPath.Application.IClients
{
    public record PaymentLinkResult(string CheckoutUrl, string PaymentLinkId, string QrCode);

    public record PaymentCallbackResult(bool IsValidSignature, bool IsSuccess, long OrderCode, decimal Amount, string Description);
    public record PaymentLinkInformation(long OrderCode, decimal Amount, decimal AmountPaid, string Status, string PaymentLinkId);

    /// <summary>Why a payment-gateway call failed, so callers can answer the learner precisely without leaking provider configuration.</summary>
    public enum PaymentGatewayFailure
    {
        NotConfigured,
        AuthenticationFailed,
        DuplicateOrderCode,
        Rejected,
        RateLimited,
        Timeout,
        Network,
        ProviderUnavailable,
        InvalidResponse,
    }

    /// <summary>
    /// A classified, sanitized gateway failure. <see cref="ProviderCode"/>/<see cref="ProviderMessage"/> are the provider's own
    /// short error code and description (never keys, headers or the request payload) and are safe to log.
    /// </summary>
    public sealed class PaymentGatewayException(
        PaymentGatewayFailure failure, string message, int? httpStatus = null, string? providerCode = null,
        string? providerMessage = null, Exception? inner = null) : Exception(message, inner)
    {
        public PaymentGatewayFailure Failure { get; } = failure;
        public int? HttpStatus { get; } = httpStatus;
        public string? ProviderCode { get; } = providerCode;
        public string? ProviderMessage { get; } = providerMessage;

        /// <summary>Whether the link may or may not exist at the provider (the request was sent but no definitive answer came back).</summary>
        public bool OutcomeUnknown => Failure is PaymentGatewayFailure.Timeout or PaymentGatewayFailure.Network
            or PaymentGatewayFailure.ProviderUnavailable or PaymentGatewayFailure.InvalidResponse;
    }

    public interface IPaymentGatewayClient
    {
        Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken);
        // Tạo link có hạn dùng (PayOS: trường tùy chọn "expiredAt", Unix giây; không thuộc chữ ký).
        // Mặc định bỏ qua hạn dùng để các cổng/giả lập cũ vẫn hoạt động.
        Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            DateTime? expiresAtUtc,
            CancellationToken cancellationToken)
            => CreatePaymentLinkAsync(orderCode, amount, description, returnUrl, cancelUrl, cancellationToken);
        PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook);

        /// <summary>
        /// Verifies the webhook signature over the raw <c>data</c> object exactly as PayOS sent it (all keys, sorted),
        /// so fields PayOS adds later cannot break verification. Falls back to the typed check when the raw JSON is unavailable.
        /// </summary>
        PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook, string? rawDataJson)
            => VerifyWebhook(webhook);

        Task<PaymentLinkInformation> GetPaymentInformationAsync(long orderCode, CancellationToken cancellationToken);

        /// <summary>Looks a payment link up by its provider id (PayOS accepts either the order code or the paymentLinkId).</summary>
        Task<PaymentLinkInformation> GetPaymentInformationAsync(string paymentRequestId, CancellationToken cancellationToken)
            => long.TryParse(paymentRequestId, out var code)
                ? GetPaymentInformationAsync(code, cancellationToken)
                : throw new PaymentGatewayException(PaymentGatewayFailure.Rejected, "Gateway cannot look links up by id.");

        /// <summary>Vô hiệu hóa/hủy link thanh toán trên cổng khi giữ chỗ hết hạn hoặc người dùng hủy (best-effort).</summary>
        Task<bool> CancelPaymentLinkAsync(long orderCode, string? cancellationReason, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
