namespace RikiPath.Application.Requests.MentorMeetings;
public sealed record SaveMentorPlanRequest(string Name, string? Description, int MeetingSessionCount, decimal Price, bool IsActive);
