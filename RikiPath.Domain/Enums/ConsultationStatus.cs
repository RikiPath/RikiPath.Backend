namespace RikiPath.Domain.Enums
{
    /// <summary>Lifecycle of a purchased consultation request.</summary>
    public enum ConsultationStatus
    {
        AwaitingPayment,
        PendingAssignment,
        Assigned,
        Accepted,
        Completed,
        Cancelled
    }
}
