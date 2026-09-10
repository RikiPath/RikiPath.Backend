using Domain.Enums;

namespace RikiPath.Application.Responses.CoursePurchases
{
    public class CoursePurchaseStatusResponse
    {
        public int PurchaseId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public PaymentStatus PaymentStatus { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}
