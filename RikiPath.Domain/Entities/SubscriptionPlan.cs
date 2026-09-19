namespace RikiPath.Domain.Entities
{
    public class SubscriptionPlan : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }

        /// <summary>Số lượt chấm AI Writing &amp; Speaking đi kèm gói, trong suốt DurationDays.</summary>
        public int AiGradingQuota { get; set; }

        /// <summary>Đánh dấu gói "phổ biến nhất" để FE highlight — hiện tại là gói 1 tháng.</summary>
        public bool IsPopular { get; set; } = false;
        public bool IsTrial { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public List<UserSubscription>? UserSubscriptions { get; set; }
    }
}
