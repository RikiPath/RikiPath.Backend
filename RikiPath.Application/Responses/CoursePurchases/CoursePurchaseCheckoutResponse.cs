namespace RikiPath.Application.Responses.CoursePurchases
{
    public class CoursePurchaseCheckoutResponse
    {
        public int PurchaseId { get; set; }
        public long OrderCode { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
