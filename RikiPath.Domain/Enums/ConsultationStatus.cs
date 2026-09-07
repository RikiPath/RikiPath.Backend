namespace Domain.Enums
{
    /// <summary>Lifecycle of a purchased consultation request.</summary>
    public enum ConsultationStatus
    {
        PendingAssignment,
        Assigned,
        Accepted,
        Completed,
        Cancelled
    }
}
