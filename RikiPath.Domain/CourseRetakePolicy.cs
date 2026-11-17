using RikiPath.Domain.Entities;

namespace RikiPath.Domain;

public sealed record CourseEligibility(int RequiredHomework, int PassedOnTimeHomework, int CountedSessions,
    int AttendedSessions, int AbsentSessions, bool HomeworkMet, bool AttendanceMet, bool CanClaim, string[] Reasons);

public static class CourseRetakePolicy
{
    public static CourseEligibility Evaluate(
        StudyCourse course,
        bool alreadyRetaken,
        MentorGroupPolicy? policy = null,
        IReadOnlyCollection<HomeworkAssignment>? cohortAssignments = null)
    {
        var required = course.Homework.Where(x => x.Required).ToList();
        var requiredCount = required.Count;
        var passed = required.Count(x => x.SubmittedAt.HasValue && x.SubmittedAt <= x.DueAt);

        if (cohortAssignments is { Count: > 0 })
        {
            var scopedAssignments = cohortAssignments
                .Where(a => !a.IsDeleted && a.StudyCourseId == course.Id)
                .ToList();
            requiredCount += scopedAssignments.Count;
            passed += scopedAssignments.Count(a => a.Submission is { IsDeleted: false } sub
                && (!a.DueAt.HasValue || sub.SubmittedAt <= a.DueAt.Value));
        }

        var logicalSessions = course.Sessions.GroupBy(x => x.Number).ToList();
        var counted = logicalSessions.Count(g => g.Any(x => x.Status is "Present" or "Absent" or "ExcusedAbsent"));
        var attended = logicalSessions.Count(g => g.Any(x => x.Status == "Present"));
        var absent = counted - attended;

        var effectivePolicy = policy ?? course.GroupPolicy;
        var thresholdPercent = effectivePolicy?.RemediationCompletionThresholdPercent ?? course.MinimumHomeworkPercent;
        var strictGreater = string.Equals(effectivePolicy?.RemediationThresholdComparison, ">", StringComparison.Ordinal);
        var homeworkMet = requiredCount > 0 && (strictGreater
            ? (decimal)passed * 100m > (decimal)requiredCount * thresholdPercent
            : (decimal)passed * 100m >= (decimal)requiredCount * thresholdPercent);
        var attendanceMet = true; // Absence is not an eligibility condition.
        var reasons = new List<string>();
        if (course.RetakeOfId.HasValue) reasons.Add("Khóa học lại không phát sinh quyền học lại tiếp.");
        if (course.PaymentStatus != "Paid" || course.Status != "Completed") reasons.Add("Khóa phải đã thanh toán và hoàn thành.");
        if (alreadyRetaken) reasons.Add("Khóa gốc đã sử dụng quyền học lại.");
        if (!homeworkMet)
        {
            var op = strictGreater ? ">" : ">=";
            reasons.Add($"Chưa có đủ dữ liệu xác nhận {op} {thresholdPercent:0.##}% BTVN nộp đúng hạn; chờ Admin kiểm tra minh chứng.");
        }

        if (logicalSessions.Count != course.TotalSessions || course.Sessions.Any(x => x.Status == "Scheduled"))
            reasons.Add("Chưa chốt toàn bộ buổi học của khóa.");
        return new(requiredCount, passed, counted, attended, absent, homeworkMet, attendanceMet, reasons.Count == 0, reasons.ToArray());
    }
}
