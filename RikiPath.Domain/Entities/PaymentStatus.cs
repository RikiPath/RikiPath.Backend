namespace Domain.Entities
{
    /// <summary>Payment status for a consultation-package purchase.</summary>
    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }
}
