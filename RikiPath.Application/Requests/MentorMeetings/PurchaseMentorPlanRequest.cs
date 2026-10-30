namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class PurchaseMentorPlanRequest
    {
        public int SubscriptionPlanId { get; set; }
        /// <summary>Origin của trang đang thanh toán, ví dụ https://riki-path-web.vercel.app. PayOS dùng origin này để quay lại biên lai.</summary>
        public string? FrontendOrigin { get; set; }
        public Guid? IdempotencyKey { get; set; }
    }
}
