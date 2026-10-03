namespace RikiPath.Domain.Enums
{
    /// <summary>Lifecycle of a purchased consultation request.</summary>
    public enum MentorStatus
    {
        AwaitingPayment,
        PendingAssignment,
        Assigned,
        Accepted,
        Completed,
        Cancelled
    }
}
