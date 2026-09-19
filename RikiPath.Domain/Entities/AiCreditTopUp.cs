using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class AiCreditTopUp : Base
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? PaymentTransactionId { get; set; }

        public DateTime? ActivatedAt { get; set; }
    }
}
