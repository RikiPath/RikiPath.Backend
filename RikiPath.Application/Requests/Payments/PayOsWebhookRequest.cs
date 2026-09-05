using System.Text.Json.Serialization;

namespace RikiPath.Application.Requests.Payments
{
    // Shape theo doc PayOS (Webhook): { code, desc, success, data: {...}, signature }
    // -> kiểm tra lại với doc mới nhất của PayOS trước khi lên production, field có thể đổi.
    public class PayOsWebhookRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public bool Success { get; set; }
        public PayOsWebhookData Data { get; set; } = new();
        public string Signature { get; set; } = string.Empty;
    }

    public class PayOsWebhookData
    {
        public long OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public string? Reference { get; set; }
        public string? TransactionDateTime { get; set; }
        public string Currency { get; set; } = "VND";
        public string PaymentLinkId { get; set; } = string.Empty;

        // "00" = giao dịch thành công theo quy ước PayOS
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;

        public string? CounterAccountBankId { get; set; }
        public string? CounterAccountBankName { get; set; }
        public string? CounterAccountName { get; set; }
        public string? CounterAccountNumber { get; set; }
        public string? VirtualAccountName { get; set; }
        public string? VirtualAccountNumber { get; set; }
    }
}
