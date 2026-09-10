namespace RikiPath.Application.Responses.CoursePurchases
{
    public class PurchasedCourseResponse
    {
        public int PurchaseId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}
