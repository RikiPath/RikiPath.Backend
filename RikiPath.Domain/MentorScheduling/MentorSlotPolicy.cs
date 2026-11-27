namespace RikiPath.Domain.MentorScheduling;

public enum MentorDaypart
{
    Morning,
    Noon,
    Afternoon,
    Evening
}

public sealed record MentorDaypartWindow(MentorDaypart Daypart, TimeOnly StartLocal, TimeOnly EndLocal);

// // Quy tắc tạo khung giờ 90 phút, nghỉ 15 phút; không diễn giải lại lịch và buổi đặt trước đây.
public sealed class MentorSlotPolicy
{
    public const string BusinessTimeZoneId = "Asia/Ho_Chi_Minh";
    public const int AppointmentDurationMinutes = 90;
    public const int MinimumRestMinutes = 15;
    public const int DefaultSingleSlotHorizonDays = 180;
    public const int MinimumBookingLeadHours = 2;

    /// <summary>Số ngày tối thiểu giữa thời điểm mở tuyển sinh và hạn đóng tuyển sinh.</summary>
    public const int MinimumPublishedEnrollmentDays = 7;

    /// <summary>Số ngày lịch chuẩn bị tối thiểu sau ngày đóng tuyển sinh trước buổi học đầu tiên (tối thiểu 1 ngày).</summary>
    public const int MinimumPreparationDays = 1;

    /// <summary>Giới hạn kỹ thuật số tuần tối đa cho một khóa học để tránh tràn lịch/lạm dụng hệ thống (không phải quota đào tạo).</summary>
    public const int MinCourseDurationWeeks = 1;
    public const int MaxCourseDurationWeeksTechnical = 104;
    public const int MaxTotalCourseSessionsTechnical = 1000;
    public const int MaxScheduleSearchDays = 365;
    public const int MaxScheduleSearchWeeks = 52;

    /// <summary>
    /// Bậc trình độ JLPT chuẩn để so sánh điều kiện giảng dạy (không so sánh chuỗi máy móc):
    /// N5 = 1, N4 = 2, N3 = 3, N2 = 4, N1 = 5; không hợp lệ = 0.
    /// </summary>
    public static int GetJlptRank(string? code) => (code?.Trim().ToUpperInvariant()) switch
    {
        "N5" => 1,
        "N4" => 2,
        "N3" => 3,
        "N2" => 4,
        "N1" => 5,
        _ => 0,
    };

    public static string? GetJlptCodeFromRank(int rank) => rank switch
    {
        1 => "N5",
        2 => "N4",
        3 => "N3",
        4 => "N2",
        5 => "N1",
        _ => null,
    };

    /// <summary>Phạm vi mở lớp nhóm của hệ thống là N5–N2 (rank 1..4).</summary>
    public static bool IsSupportedClassLevel(string? code) => GetJlptRank(code) is >= 1 and <= 4;

    /// <summary>
    /// Giáo viên chỉ được đăng ký giảng dạy cấp độ N5–N2 có bậc bằng hoặc thấp hơn trình độ chuyên môn đã xác minh.
    /// N1/N2 -> N5..N2; N3 -> N5..N3; N4 -> N5..N4; N5 -> N5; chưa xác minh (0) -> không được mở lớp.
    /// </summary>
    public static bool CanMentorTeachLevel(int verifiedMentorRank, string? targetLevelCode)
    {
        var targetRank = GetJlptRank(targetLevelCode);
        return verifiedMentorRank > 0 && targetRank is >= 1 and <= 4 && targetRank <= verifiedMentorRank;
    }

    /// <summary>
    /// Chuẩn hóa sinh lịch định kỳ theo đúng <paramref name="durationWeeks"/> chu kỳ 7 ngày liên tiếp
    /// neo tại (<paramref name="anchorDate"/>, <paramref name="anchorStartMinute"/>).
    /// Khi một tuần có nhiều buổi hoặc nhiều buổi trong cùng ngày, các ca có giờ bắt đầu sớm hơn
    /// <paramref name="anchorStartMinute"/> trong ngày neo được đặt sang ngày +7 của chu kỳ để bảo toàn đủ
    /// toàn bộ số buổi mỗi chu kỳ mà không làm mất buổi hoặc nhảy cóc tuần.
    /// </summary>
    public static int GetMondayOffset(int weekday) => ((weekday - 1) % 7 + 7) % 7;

    public static DateOnly GetMondayOfWeek(DateOnly date) => date.AddDays(-GetMondayOffset((int)date.DayOfWeek));

    /// <summary>
    /// Quy đổi hạn tuyển sinh (chọn theo ngày tại Việt Nam) sang cuối ngày 23:59:59.999+07:00 ở dạng UTC.
    /// </summary>
    public static DateTime ResolveEnrollmentDeadlineEndOfDayUtc(DateOnly localDeadlineDate, TimeZoneInfo? businessZone = null)
    {
        var tz = businessZone ?? TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
        var localEnd = DateTime.SpecifyKind(
            localDeadlineDate.ToDateTime(new TimeOnly(23, 59, 59, 999)),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
    }

    /// <summary>
    /// Tìm Thứ 2 đầu tuần sớm nhất sao cho toàn bộ các buổi trong tuần đầu tiên đều >= <paramref name="earliestAllowedSessionDate"/>.
    /// Mỗi tuần giảng dạy luôn trọn vẹn từ Thứ 2 đến Chủ nhật, không cắt bớt buổi đầu tuần.
    /// </summary>
    public static DateOnly ResolveEarliestValidWeekMonday(
        DateOnly earliestAllowedSessionDate,
        IReadOnlyList<(int Weekday, int StartMinute)> weeklySessions)
    {
        var weekMonday = GetMondayOfWeek(earliestAllowedSessionDate);
        if (weeklySessions is null || weeklySessions.Count == 0)
            return weekMonday;

        var earliestSessionDateInCandidateWeek = weeklySessions
            .Select(s => weekMonday.AddDays(GetMondayOffset(s.Weekday)))
            .Min();

        return earliestSessionDateInCandidateWeek < earliestAllowedSessionDate
            ? weekMonday.AddDays(7)
            : weekMonday;
    }

    /// <summary>
    /// Sinh lịch học chính thức theo từng tuần đầy đủ từ Thứ 2 đến Chủ nhật trong <paramref name="durationWeeks"/> tuần liên tiếp.
    /// </summary>
    public static List<(int SessionNumber, int WeekNumber, int Weekday, DateOnly LocalDate, int StartMinute, DateTime StartUtc, DateTime EndUtc)>
        ExpandFullCalendarWeeks(
            DateOnly startWeekMonday,
            IReadOnlyList<(int Weekday, int StartMinute)> weeklySessions,
            int durationWeeks,
            int sessionMinutes,
            TimeZoneInfo? businessZone = null)
    {
        if (weeklySessions is null || weeklySessions.Count == 0
            || durationWeeks < MinCourseDurationWeeks || durationWeeks > MaxCourseDurationWeeksTechnical
            || sessionMinutes <= 0)
            return [];

        var normalizedMonday = GetMondayOfWeek(startWeekMonday);
        var uniqueSlots = weeklySessions
            .Distinct()
            .Select(s => (s.Weekday, s.StartMinute, MondayOffset: GetMondayOffset(s.Weekday)))
            .OrderBy(x => x.MondayOffset)
            .ThenBy(x => x.StartMinute)
            .ToList();

        if (uniqueSlots.Count * (long)durationWeeks > MaxTotalCourseSessionsTechnical)
            return [];

        var tz = businessZone ?? TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
        var result = new List<(int, int, int, DateOnly, int, DateTime, DateTime)>(durationWeeks * uniqueSlots.Count);
        var sessionNum = 0;

        for (var weekIdx = 0; weekIdx < durationWeeks; weekIdx++)
        {
            var weekMonday = normalizedMonday.AddDays(weekIdx * 7);
            foreach (var item in uniqueSlots)
            {
                var localDate = weekMonday.AddDays(item.MondayOffset);
                var localDt = DateTime.SpecifyKind(
                    localDate.ToDateTime(TimeOnly.MinValue).AddMinutes(item.StartMinute),
                    DateTimeKind.Unspecified);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localDt, tz);
                var endUtc = startUtc.AddMinutes(sessionMinutes);
                sessionNum++;
                result.Add((sessionNum, weekIdx + 1, item.Weekday, localDate, item.StartMinute, startUtc, endUtc));
            }
        }

        return result;
    }

    public static List<(int SessionNumber, int WeekNumber, int Weekday, DateOnly LocalDate, int StartMinute, DateTime StartUtc, DateTime EndUtc)>
        ExpandRecurringScheduleAnchored(
            DateOnly anchorDate,
            int anchorStartMinute,
            IReadOnlyList<(int Weekday, int StartMinute)> weeklySessions,
            int durationWeeks,
            int sessionMinutes,
            TimeZoneInfo? businessZone = null)
    {
        if (weeklySessions is null || weeklySessions.Count == 0
            || durationWeeks < MinCourseDurationWeeks || durationWeeks > MaxCourseDurationWeeksTechnical
            || sessionMinutes <= 0)
            return [];

        var uniqueSlots = weeklySessions.Distinct().ToList();
        if (uniqueSlots.Count * (long)durationWeeks > MaxTotalCourseSessionsTechnical)
            return [];

        var tz = businessZone ?? TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
        var anchorWeekday = (int)anchorDate.DayOfWeek;

        var ordered = uniqueSlots
            .Select(s =>
            {
                var offset = ((s.Weekday - anchorWeekday) % 7 + 7) % 7;
                if (offset == 0 && s.StartMinute < anchorStartMinute)
                    offset = 7;
                return (s.Weekday, s.StartMinute, Offset: offset);
            })
            .OrderBy(x => x.Offset)
            .ThenBy(x => x.StartMinute)
            .ToList();

        var result = new List<(int, int, int, DateOnly, int, DateTime, DateTime)>(durationWeeks * ordered.Count);
        var sessionNum = 0;

        for (var cycle = 0; cycle < durationWeeks; cycle++)
        {
            var cycleAnchor = anchorDate.AddDays(cycle * 7);
            foreach (var item in ordered)
            {
                var localDate = cycleAnchor.AddDays(item.Offset);
                var localDt = DateTime.SpecifyKind(
                    localDate.ToDateTime(TimeOnly.MinValue).AddMinutes(item.StartMinute),
                    DateTimeKind.Unspecified);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localDt, tz);
                var endUtc = startUtc.AddMinutes(sessionMinutes);
                sessionNum++;
                result.Add((sessionNum, cycle + 1, item.Weekday, localDate, item.StartMinute, startUtc, endUtc));
            }
        }

        return result;
    }

    public static List<(int SessionNumber, int WeekNumber, int Weekday, DateOnly LocalDate, int StartMinute, DateTime StartUtc, DateTime EndUtc)>
        ExpandRecurringSchedule(
            DateOnly expectedStartDate,
            IReadOnlyList<(int Weekday, int StartMinute)> weeklySessions,
            int durationWeeks,
            int sessionMinutes,
            TimeZoneInfo? businessZone = null)
        => ExpandRecurringScheduleAnchored(expectedStartDate, 0, weeklySessions, durationWeeks, sessionMinutes, businessZone);

    private static readonly MentorDaypartWindow[] DefaultWindows =
    [
        new(MentorDaypart.Morning, new TimeOnly(7, 0), new TimeOnly(11, 0)),
        new(MentorDaypart.Noon, new TimeOnly(11, 0), new TimeOnly(13, 0)),
        new(MentorDaypart.Afternoon, new TimeOnly(13, 0), new TimeOnly(18, 0)),
        new(MentorDaypart.Evening, new TimeOnly(18, 0), new TimeOnly(22, 0))
    ];

    public MentorSlotPolicy(
        IEnumerable<MentorDaypartWindow>? dayparts = null,
        int dailyBookingCap = 4,
        bool autoPublicationEnabled = false)
    {
        DailyBookingCap = dailyBookingCap;
        AutoPublicationEnabled = autoPublicationEnabled;
        Dayparts = Array.AsReadOnly((dayparts ?? DefaultWindows).OrderBy(x => x.StartLocal).ToArray());

        if (DailyBookingCap < 1)
            throw new ArgumentOutOfRangeException(nameof(dailyBookingCap), "Giới hạn số buổi hỗ trợ mỗi ngày phải lớn hơn 0.");
        if (Dayparts.Count == 0)
            throw new ArgumentException("Cần cấu hình ít nhất một ca làm việc.", nameof(dayparts));
        if (Dayparts.Select(x => x.Daypart).Distinct().Count() != Dayparts.Count)
            throw new ArgumentException("Mỗi ca làm việc chỉ được cấu hình một lần.", nameof(dayparts));

        foreach (var window in Dayparts)
        {
            if (!Enum.IsDefined(window.Daypart))
                throw new ArgumentException("Ca làm việc không hợp lệ.", nameof(dayparts));
            if (window.StartLocal >= window.EndLocal)
                throw new ArgumentException($"Giờ kết thúc ca {window.Daypart} phải sau giờ bắt đầu.", nameof(dayparts));
            if (window.StartLocal.Ticks % TimeSpan.TicksPerMinute != 0
                || window.EndLocal.Ticks % TimeSpan.TicksPerMinute != 0)
                throw new ArgumentException("Giờ bắt đầu và kết thúc ca làm việc phải chính xác đến phút.", nameof(dayparts));
        }

        for (var i = 1; i < Dayparts.Count; i++)
        {
            if (Dayparts[i - 1].EndLocal > Dayparts[i].StartLocal)
                throw new ArgumentException("Các ca làm việc được cấu hình không được trùng thời gian.", nameof(dayparts));
        }

        BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
    }

    public IReadOnlyList<MentorDaypartWindow> Dayparts { get; }
    public int DailyBookingCap { get; }
    public bool AutoPublicationEnabled { get; }
    public TimeZoneInfo BusinessTimeZone { get; }

    public bool TryGetDaypart(MentorDaypart daypart, out MentorDaypartWindow window)
    {
        var found = Dayparts.FirstOrDefault(x => x.Daypart == daypart);
        window = found!;
        return found is not null;
    }

    public DateOnly GetBusinessDate(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, BusinessTimeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public DateTimeOffset ToUtc(DateOnly date, TimeOnly localTime)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(localTime), DateTimeKind.Unspecified);
        if (BusinessTimeZone.IsInvalidTime(local) || BusinessTimeZone.IsAmbiguousTime(local))
            throw new ArgumentException("Giờ làm việc không tồn tại hoặc bị trùng theo múi giờ áp dụng.");

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, BusinessTimeZone));
    }
}

// Khung giờ dự kiến chỉ nằm trong bộ nhớ, chưa được lưu hoặc công bố.
public sealed record MentorGeneratedSlot(
    int MentorId,
    DateOnly LocalDate,
    MentorDaypart Daypart,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int PolicyVersion,
    string GenerationKey);

// // Dữ liệu buổi hẹn hiện có do bên gọi cung cấp; buổi đã hoàn thành vẫn tính vào giới hạn ngày.
public sealed record ExistingMentorAppointment(
    int MentorId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool UsesNinetyMinutePolicy,
    bool IsCancelled);

public sealed record MentorBookingCandidate(
    int MentorId,
    MentorDaypart Daypart,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int PolicyVersion = 3);

public sealed record MentorSlotValidationResult(bool IsValid, string? ErrorCode = null, string? Message = null)
{
    public static MentorSlotValidationResult Valid() => new(true);
    public static MentorSlotValidationResult Invalid(string code, string message) => new(false, code, message);
}
