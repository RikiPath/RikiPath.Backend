namespace RikiPath.Domain.Enums
{
    /// <summary>Trạng thái vòng đời của 1 bản nháp (ContentApprovalRequest) do Content Author import lên.</summary>
    public enum ContentApprovalStatus
    {
        Draft,
        PendingReview,
        Approved,
        Rejected,
    }
}
