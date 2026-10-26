namespace RikiPath.Application.Responses.MentorMeetings;
public sealed record WeeklyAvailabilityResponse(int Id, int MentorId, string MentorName, DateOnly Date, string Period);
