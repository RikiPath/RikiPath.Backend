namespace RikiPath.Application.Responses.MentorMeetings;

public sealed record AdminMentorScheduleBookingResponse(int Id, int LearnerId, string LearnerName,
    string? Question, string Status, string PaymentStatus);
public sealed record AdminMentorScheduleResponse(int Id, DateTime StartTime, DateTime EndTime,
    string Status, List<AdminMentorScheduleBookingResponse> Bookings);
