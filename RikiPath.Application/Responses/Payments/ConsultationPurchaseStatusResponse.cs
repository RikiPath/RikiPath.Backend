using Domain.Enums;

namespace RikiPath.Application.Responses.Payments
{
    public class ConsultationPurchaseStatusResponse
    {
        public int Id { get; set; }
        public int ConsultationPackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public PaymentStatus Status { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}
