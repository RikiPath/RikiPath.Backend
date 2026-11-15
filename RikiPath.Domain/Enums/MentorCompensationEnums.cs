namespace RikiPath.Domain.Enums;

public enum MentorAttendanceApprovalStatus
{
    PendingAdminReview = 0,
    ApprovedForEarning = 1,
    HeldDisputed = 2,
    RejectedNoEarning = 3,
    LegacyIneligible = 4
}

public enum MentorEarningStatus
{
    Eligible = 0,
    HeldDisputed = 1,
    LockedInPayroll = 2,
    Paid = 3
}

public enum MentorPayrollStatus
{
    Draft = 0,
    Review = 1,
    Approved = 2,
    Paid = 3,
    Closed = 4
}

public enum MentorBankVerificationStatus
{
    PendingVerification = 0,
    Verified = 1,
    Rejected = 2,
    Superseded = 3
}

public enum PayoutExecutionMethod
{
    ManualBankTransfer = 0,
    PayOsPayout = 1
}

public enum MentorPayoutObligationStatus
{
    PendingExecution = 0,
    Processing = 1,
    UnknownPendingReconciliation = 2,
    OnHoldMissingEvidence = 3,
    Paid = 4,
    FailedRetryable = 5,
    Cancelled = 6
}

public enum MentorPayoutAttemptStatus
{
    Initiated = 0,
    Processing = 1,
    UnknownPendingReconciliation = 2,
    OnHoldMissingEvidence = 3,
    Succeeded = 4,
    Failed = 5
}
