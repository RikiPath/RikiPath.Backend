namespace RikiPath.Domain.Enums
{
    /// <summary>
    /// Payment status of a purchase order. Persisted as a string (no ordinal storage), so new values are appended
    /// without a schema change. Pending → Paid | Failed | Cancelled | Expired; Paid → Refunded.
    /// </summary>
    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded,
        /// <summary>The learner cancelled on the hosted checkout (verified server-to-server with the provider).</summary>
        Cancelled,
        /// <summary>The payment link expired at the provider without being paid.</summary>
        Expired,
    }
}
