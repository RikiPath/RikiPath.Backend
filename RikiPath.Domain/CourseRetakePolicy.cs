using RikiPath.Domain.Entities;

namespace RikiPath.Domain;

public sealed record CourseEligibility(int RequiredHomework, int PassedOnTimeHomework, int CountedSessions,
    int AttendedSessions, int AbsentSessions, bool HomeworkMet, bool AttendanceMet, bool CanClaim, string[] Reasons);

public static class CourseRetakePolicy
{
    public static CourseEligibility Evaluate(StudyCourse course, bool alreadyRetaken)
    {
        var required = course.Homework.Where(x => x.Required).ToList();
        var passed = required.Count(x => x.SubmittedAt.HasValue && x.SubmittedAt <= x.DueAt);
        var logicalSessions = course.Sessions.GroupBy(x => x.Number).ToList();
        var counted = logicalSessions.Count(g => g.Any(x => x.Status is "Present" or "Absent" or "ExcusedAbsent"));
        var attended = logicalSessions.Count(g => g.Any(x => x.Status == "Present"));
        var absent = counted - attended;
        var homeworkMet = required.Count > 0 && (long)passed * 100 >= (long)required.Count * course.MinimumHomeworkPercent;
        var attendanceMet = true; // Absence is not an eligibility condition.
        var reasons = new List<string>();
        if (course.RetakeOfId.HasValue) reasons.Add("Khóa học lại không phát sinh quyền học lại tiếp.");
        if (course.PaymentStatus != "Paid" || course.Status != "Completed") reasons.Add("Khóa phải đã thanh toán và hoàn thành.");
        if (alreadyRetaken) reasons.Add("Khóa gốc đã sử dụng quyền học lại.");
        if (!homeworkMet) reasons.Add("Chưa có đủ dữ liệu xác nhận 90% BTVN nộp đúng hạn; chờ Admin kiểm tra minh chứng.");

        if (logicalSessions.Count != course.TotalSessions || course.Sessions.Any(x => x.Status == "Scheduled"))
            reasons.Add("Chưa chốt toàn bộ buổi học của khóa.");
        return new(required.Count, passed, counted, attended, absent, homeworkMet, attendanceMet, reasons.Count == 0, reasons.ToArray());
    }
}
