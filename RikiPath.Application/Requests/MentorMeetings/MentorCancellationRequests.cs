namespace RikiPath.Application.Requests.MentorMeetings;

public sealed class CancelMentorBookingRequest
{
    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool RequestException { get; set; }
    public string? ExceptionReason { get; set; }
}

public sealed class RescheduleMentorBookingRequest
{
    public int NewSlotId { get; set; }
    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class ReviewCancellationExceptionRequest
{
    public bool ApproveRefund { get; set; }
    public string? AdminNotes { get; set; }
    public int ExtendDays { get; set; } = 7;
}
