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
    public const int MaximumScheduleHorizonDays = 30;
    public const int MinimumBookingLeadHours = 2;
    public const int DefaultDailyBookingCap = 4;

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
