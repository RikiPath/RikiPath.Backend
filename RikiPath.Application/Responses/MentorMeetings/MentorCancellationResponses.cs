using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class MentorCancellationResponse
{
    public int BookingId { get; set; }
    public MentorStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    public string CancelledBy { get; set; } = string.Empty;
    public DateTime CancelledAtUtc { get; set; }
    public bool IsRefunded { get; set; }
    public string? RefundOperationKey { get; set; }
    public bool IsLateCancellation { get; set; }
    public bool ExceptionRequested { get; set; }
    public bool? ExceptionApproved { get; set; }
    public int? ReleasedSlotId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class MentorRescheduleResponse
{
    public int BookingId { get; set; }
    public int PreviousSlotId { get; set; }
    public int NewSlotId { get; set; }
    public DateTime PreviousScheduledAtUtc { get; set; }
    public DateTime NewScheduledAtUtc { get; set; }
    public MentorStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CancellationPolicyEligibilityResponse
{
    public int BookingId { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReschedule { get; set; }
    public bool EligibleForFullRefund { get; set; }
    public double HoursUntilStart { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
