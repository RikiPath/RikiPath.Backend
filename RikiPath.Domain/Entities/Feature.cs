using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>A catalog feature governed by centralized entitlement policy and optional subscription plan bindings.</summary>
    public class Feature : Base
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "general";
        public string AccessMode { get; set; } = FeatureAccessModes.Free;
        public bool IsCoreFree { get; set; }
        public string? CreditFeatureKey { get; set; }
        public string? RoutePrefixesJson { get; set; }
        public int SortOrder { get; set; }
        public List<SubscriptionPlan> SubscriptionPlans { get; set; } = [];
    }
}
