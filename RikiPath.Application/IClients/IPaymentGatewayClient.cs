using RikiPath.Application.Requests.Payments;

namespace RikiPath.Application.IClients
{
    public record PaymentLinkResult(string CheckoutUrl, string PaymentLinkId, string QrCode);

    public record PaymentCallbackResult(bool IsValidSignature, bool IsSuccess, long OrderCode, decimal Amount, string Description);
    public record PaymentLinkInformation(long OrderCode, decimal Amount, decimal AmountPaid, string Status, string PaymentLinkId);

    public interface IPaymentGatewayClient
    {
        Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken);
        PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook);
        Task<PaymentLinkInformation> GetPaymentInformationAsync(long orderCode, CancellationToken cancellationToken);
    }
}
