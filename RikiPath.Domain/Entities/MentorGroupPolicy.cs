namespace RikiPath.Domain.Entities;

// Chính sách chỉ thêm phiên bản mới; buổi đã công bố giữ nguyên sức chứa và phiên bản áp dụng.
public sealed class MentorGroupPolicy
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public int MaxLearners { get; set; }
    public int EnrollmentCutoffMinutesBeforeFirstSession { get; set; }
    public int SessionMinutes { get; set; }
    public int RestMinutes { get; set; }
    public int DailySessionCap { get; set; }
    // Giới hạn số buổi dạy của Mentor trong một tuần (giờ Việt Nam, bắt đầu thứ 2); 0 = không giới hạn.
    public int WeeklySessionCap { get; set; }
    public int PublicationHorizonDays { get; set; }
    public int CancellationLeadHours { get; set; }
    public int ReschedulingLeadHours { get; set; }
    public int HomeworkDeadlineDays { get; set; }
    public int PlacementAttemptLimit { get; set; }
    public decimal RemediationCompletionThresholdPercent { get; set; }
    public string RemediationThresholdComparison { get; set; } = ">";
    public bool RequireOfficialExamEvidence { get; set; } = true;
    public bool RequireAdminRemediationReview { get; set; } = true;
    public int KpiReportingPeriodDays { get; set; }
    // Chuyển lớp: luôn cần Admin duyệt. Chuyển vào lớp đã khai giảng không được hỗ trợ (chờ Product Owner).
    public bool LearnerTransferEnabled { get; set; } = true;
    // Cho phép Học viên có lớp nguồn đã khai giảng chuyển sang lớp CHƯA khai giảng (mặc định tắt).
    public bool TransferAllowedAfterSourceStart { get; set; }
    public bool MentorReplacementEnabled { get; set; } = true;
    // Buổi đầu tiên chuyển cho Mentor thay thế phải bắt đầu sau ít nhất số giờ này.
    public int MentorReplacementLeadHours { get; set; } = 24;
    public string RecurringWeekdayPatternsJson { get; set; } = "[]";
    // Khóa học nhóm chuẩn (quyết định Product Owner): 10 tuần; cường độ 2/3/4 buổi mỗi tuần (Nhẹ/Tiêu chuẩn/Nâng cao).
    // Cường độ KHÔNG phải cấp độ JLPT.
    public int CourseDurationWeeks { get; set; } = 10;
    public string AllowedSessionsPerWeekCsv { get; set; } = "2,3,4";
    public int DefaultSessionsPerWeek { get; set; } = 3;
    // Thời gian giữ chỗ khi thanh toán (phút), Admin cấu hình.
    public int CheckoutHoldMinutes { get; set; } = 10;
    // Số học viên tối thiểu để lớp bắt đầu (không chờ đủ sĩ số tối đa).
    public int MinLearnersToStart { get; set; } = 1;
    public int? CreatedByAdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public IReadOnlyList<int> AllowedSessionsPerWeek() => ParseSessionsPerWeekCsv(AllowedSessionsPerWeekCsv);

    public static IReadOnlyList<int> ParseSessionsPerWeekCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        var result = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
                result.Add(value);
        return result;
    }
}
