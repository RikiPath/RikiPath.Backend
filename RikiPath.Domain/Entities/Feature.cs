namespace RikiPath.Domain.Entities
{
    /// <summary>A feature included in one or more subscription plans.</summary>
    public class Feature : Base
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<SubscriptionPlan> SubscriptionPlans { get; set; } = [];
    }
}
