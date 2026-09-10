using RikiPath.Domain.Entities;
using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class CoursePurchase : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }

        public decimal AmountPaid { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public long OrderCode { get; set; }

        public string? PaymentTransactionId { get; set; }

        public DateTime PurchasedAt { get; set; }
    }
}