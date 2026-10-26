namespace RikiPath.Application.Requests.MentorMeetings;
public sealed record WeeklyPeriodRequest(DateOnly Date, string Period);
public sealed record SaveWeeklyAvailabilityRequest(DateOnly WeekStart, List<WeeklyPeriodRequest> Periods, int? MentorId = null);
public sealed record AssignMentorSlotsRequest(int WeeklyAvailabilityId, List<int> SlotIndexes);
public sealed record ChangeMentorSlotRequest(string Reason);
public sealed record ResolveMentorSlotRequest(bool CancelSlot, string Reason);
