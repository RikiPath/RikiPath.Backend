using RikiPath.Application.IClients;
using RikiPath.Application.Requests.Payments;
using RikiPath.Domain;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RikiPath.Infrastructure.Clients
{
    public class PayOsClient(HttpClient httpClient, AppSettings appSettings) : IPaymentGatewayClient
    {
        private const string CreatePaymentLinkUrl = "https://api-merchant.payos.vn/v2/payment-requests";

        public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode, int amount, string description, string returnUrl, string cancelUrl,
            CancellationToken cancellationToken)
        {
            var payOs = appSettings.PayOs;

            // Ký trên đúng 5 field này, đúng thứ tự alphabet, theo doc PayOS "Create Payment Link".
            var signatureData =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            var signature = ComputeHmacSha256(signatureData, payOs.ChecksumKey);

            var requestBody = new { orderCode, amount, description, cancelUrl, returnUrl, signature };

            using var request = new HttpRequestMessage(HttpMethod.Post, CreatePaymentLinkUrl)
            {
                Content = JsonContent.Create(requestBody),
            };
            request.Headers.Add("x-client-id", payOs.ClientId);
            request.Headers.Add("x-api-key", payOs.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"PayOS create-payment-link HTTP {(int)response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
            if (code != "00")
            {
                var desc = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : "unknown error";
                throw new InvalidOperationException($"PayOS trả về lỗi: {code} - {desc}");
            }

            var data = root.GetProperty("data");
            return new PaymentLinkResult(
                CheckoutUrl: data.GetProperty("checkoutUrl").GetString() ?? string.Empty,
                PaymentLinkId: data.TryGetProperty("paymentLinkId", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
                QrCode: data.TryGetProperty("qrCode", out var qrEl) ? qrEl.GetString() ?? string.Empty : string.Empty);
        }

        public PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook)
        {
            var payOs = appSettings.PayOs;
            var d = webhook.Data;

            // Thứ tự field PHẢI đúng alphabet theo tên field JSON gốc của PayOS — sai thứ tự
            // là chữ ký sẽ không khớp dù key/value đúng.
            var orderedFields = new (string Key, string Value)[]
            {
                ("accountNumber", d.AccountNumber ?? ""),
                ("amount", d.Amount.ToString()),
                ("code", d.Code ?? ""),
                ("counterAccountBankId", d.CounterAccountBankId ?? ""),
                ("counterAccountBankName", d.CounterAccountBankName ?? ""),
                ("counterAccountName", d.CounterAccountName ?? ""),
                ("counterAccountNumber", d.CounterAccountNumber ?? ""),
                ("currency", d.Currency ?? ""),
                ("desc", d.Desc ?? ""),
                ("description", d.Description ?? ""),
                ("orderCode", d.OrderCode.ToString()),
                ("paymentLinkId", d.PaymentLinkId ?? ""),
                ("reference", d.Reference ?? ""),
                ("transactionDateTime", d.TransactionDateTime ?? ""),
                ("virtualAccountName", d.VirtualAccountName ?? ""),
                ("virtualAccountNumber", d.VirtualAccountNumber ?? ""),
            };

            var dataStr = string.Join("&", orderedFields.Select(f => $"{f.Key}={f.Value}"));
            var expectedSignature = ComputeHmacSha256(dataStr, payOs.ChecksumKey);

            var isValidSignature = string.Equals(expectedSignature, webhook.Signature, StringComparison.OrdinalIgnoreCase);
            // Tuyệt đối không coi giao dịch là thành công nếu chữ ký sai, kể cả khi
            // webhook.Success/d.Code báo "thành công" — có thể là giả mạo.
            var isSuccess = isValidSignature && webhook.Success && d.Code == "00";

            return new PaymentCallbackResult(isValidSignature, isSuccess, d.OrderCode, d.Amount, d.Description);
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
