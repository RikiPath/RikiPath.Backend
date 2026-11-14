using RikiPath.Domain.Entities;

namespace RikiPath.Domain;

public sealed record CourseEligibility(int RequiredHomework, int PassedOnTimeHomework, int CountedSessions,
    int AttendedSessions, int AbsentSessions, bool HomeworkMet, bool AttendanceMet, bool CanClaim, string[] Reasons);

public static class CourseRetakePolicy
{
    public static CourseEligibility Evaluate(StudyCourse course, bool alreadyRetaken)
    {
        var required = course.Homework.Where(x => x.Required).ToList();
        var passed = required.Count(x => x.Passed == true && x.SubmittedAt.HasValue && x.SubmittedAt <= x.DueAt);
        var logicalSessions = course.Sessions.GroupBy(x => x.Number).ToList();
        var counted = logicalSessions.Count(g => g.Any(x => x.Status is "Present" or "Absent" or "ExcusedAbsent"));
        var attended = logicalSessions.Count(g => g.Any(x => x.Status == "Present"));
        var absent = counted - attended;
        var homeworkMet = required.Count > 0 && (long)passed * 100 >= (long)required.Count * course.MinimumHomeworkPercent;
        var attendanceMet = counted > 0 && (long)absent * 100 <= (long)counted * course.MaximumAbsencePercent;
        var reasons = new List<string>();
        if (course.RetakeOfId.HasValue) reasons.Add("Khóa học lại không phát sinh quyền học lại tiếp.");
        if (course.PaymentStatus != "Paid" || course.Status != "Completed") reasons.Add("Khóa phải đã thanh toán và hoàn thành.");
        if (alreadyRetaken) reasons.Add("Khóa gốc đã sử dụng quyền học lại.");
        if (!homeworkMet) reasons.Add("Chưa đạt 90% BTVN bắt buộc được Mentor xác nhận đạt và nộp đúng hạn.");
        if (!attendanceMet) reasons.Add("Tỷ lệ nghỉ vượt 20% hoặc chưa có buổi điểm danh hợp lệ.");
        if (logicalSessions.Count != course.TotalSessions || course.Sessions.Any(x => x.Status == "Scheduled"))
            reasons.Add("Chưa chốt toàn bộ buổi học của khóa.");
        return new(required.Count, passed, counted, attended, absent, homeworkMet, attendanceMet, reasons.Count == 0, reasons.ToArray());
    }
}
