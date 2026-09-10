using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>A purchasable consultation offering — Admin configures pricing and duration.</summary>
    public class ConsultationPackage : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public ConsultationType Type { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsActive { get; set; } = true;

        public List<ConsultationPurchase>? Purchases { get; set; }
    }
}
