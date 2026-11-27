namespace RikiPath.Domain.Entities;

// Chính sách chỉ thêm phiên bản mới; buổi đã công bố giữ nguyên sức chứa và phiên bản áp dụng.
public sealed class MentorGroupPolicy
{
    public int Id { get; set; }
    public int Version { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsActive { get; set; }
    public bool IsArchivedRevision { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? LastAppliedAtUtc { get; set; }
    public int? LastAppliedByAdminId { get; set; }
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
    // Số ngày tối thiểu giữa thời điểm gửi đề xuất mở lớp và ngày khai giảng buổi đầu tiên.
    public int MinimumProposalLeadDays { get; set; } = 14;
    // Số ngày tối thiểu giữa thời điểm duyệt/mở tuyển sinh và ngày khai giảng buổi đầu tiên.
    public int MinimumPublishedEnrollmentDays { get; set; } = 7;
    // Đơn giá học phí trên mỗi học viên cho mỗi buổi học 90 phút (VND) theo cấp độ JLPT N5–N2.
    public decimal TuitionPerSessionN5 { get; set; } = 60_000m;
    public decimal TuitionPerSessionN4 { get; set; } = 75_000m;
    public decimal TuitionPerSessionN3 { get; set; } = 90_000m;
    public decimal TuitionPerSessionN2 { get; set; } = 110_000m;
    // Tổng số lớp/đề xuất đang hoạt động tối đa mỗi Giáo viên (độc lập với giới hạn theo từng cấp độ JLPT).
    public int MaxTotalActiveClassesPerMentor { get; set; } = 4;
    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<MentorGroupPolicyLevelLimit> LevelLimits { get; set; } = [];

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Dictionary<string, int> MaxActiveClassesByLevel => GetMaxActiveClassesByLevelCode();

    public int? CreatedByAdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedByAdminId { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByAdminId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? CreatedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? LastAppliedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? UpdatedByAdmin { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount? DeletedByAdmin { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CreatedByAdminName => FormatAdminName(CreatedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? LastAppliedByAdminName => FormatAdminName(LastAppliedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? UpdatedByAdminName => FormatAdminName(UpdatedByAdmin);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool CanDelete { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? DeleteBlockedReason { get; set; }

    private static string? FormatAdminName(UserAccount? user)
    {
        if (user is null) return null;
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email.Trim() : null);
    }

    public int GetMaxActiveClassesForLevel(int certificateLevelId, string? levelCode = null)
    {
        if (certificateLevelId > 0)
        {
            var byId = LevelLimits.FirstOrDefault(x => x.CertificateLevelId == certificateLevelId);
            if (byId is not null)
                return Math.Max(0, byId.MaxActiveClasses);
        }

        if (!string.IsNullOrWhiteSpace(levelCode))
        {
            var normalized = levelCode.Trim();
            var byCode = LevelLimits.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.CertificateLevel?.Code)
                && string.Equals(x.CertificateLevel!.Code.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (byCode is not null)
                return Math.Max(0, byCode.MaxActiveClasses);
        }

        return LevelLimits.Count > 0 || string.IsNullOrWhiteSpace(levelCode) || !MentorScheduling.MentorSlotPolicy.IsSupportedClassLevel(levelCode)
            ? 0
            : 1;
    }

    public Dictionary<string, int> GetMaxActiveClassesByLevelCode()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in LevelLimits)
        {
            var code = item.CertificateLevel?.Code?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code)) continue;
            map[code] = Math.Max(0, item.MaxActiveClasses);
        }
        return map;
    }

    public IReadOnlyList<int> AllowedSessionsPerWeek() => ParseSessionsPerWeekCsv(AllowedSessionsPerWeekCsv);

    public decimal GetTuitionPerSession(string? levelCode) => (levelCode?.Trim().ToUpperInvariant()) switch
    {
        "N5" => TuitionPerSessionN5,
        "N4" => TuitionPerSessionN4,
        "N3" => TuitionPerSessionN3,
        "N2" => TuitionPerSessionN2,
        _ => 0m,
    };

    public decimal CalculateCourseTuition(string? levelCode, int totalSessions)
    {
        if (totalSessions <= 0) return 0m;
        var perSession = GetTuitionPerSession(levelCode);
        return perSession > 0m ? decimal.Truncate(perSession) * totalSessions : 0m;
    }

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
