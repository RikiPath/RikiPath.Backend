using System;

namespace Domain.Entities
{
    /// <summary>
    /// A learner's purchase of a consultation package, processed through a PCI-compliant
    /// payment gateway — only the gateway's transaction reference is stored here, never raw card data.
    /// </summary>
    public class ConsultationPurchase : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int ConsultationPackageId { get; set; }
        public ConsultationPackage ConsultationPackage { get; set; }

        public decimal AmountPaid { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? PaymentTransactionId { get; set; }
        public DateTime PurchasedAt { get; set; }

        public ConsultationRequest? ConsultationRequest { get; set; }
    }
}
