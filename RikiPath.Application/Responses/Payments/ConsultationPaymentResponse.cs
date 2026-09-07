namespace RikiPath.Application.Responses.Payments
{
    public class ConsultationPaymentResponse
    {
        public int PurchaseId { get; set; }

        /// <summary>orderCode dùng để đối chiếu với webhook PayOS gọi về sau này.</summary>
        public long OrderCode { get; set; }

        public string CheckoutUrl { get; set; } = string.Empty;
        public string PaymentLinkId { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
