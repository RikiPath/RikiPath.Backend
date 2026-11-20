using System.Globalization;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Options;

public sealed class MentorSlotGenerationOptions
{
    public const string SectionName = "MentorSlotGeneration";

    public int DailyBookingCap { get; set; } = 4;
    /// <summary>Số khung giờ tối đa được gửi trong một lần đăng ký / batch (mặc định 14).</summary>
    public int MaxSlotsPerSubmission { get; set; } = 14;
    /// <summary>Tổng số khung giờ đang chờ Admin duyệt tối đa của một Giáo viên tại một thời điểm (mặc định 24).</summary>
    public int MaxPendingSlots { get; set; } = 24;
    /// <summary>Thời gian chờ tối thiểu (giây) giữa hai lần gửi đăng ký lịch liên tiếp (mặc định 5 giây).</summary>
    public int SubmissionCooldownSeconds { get; set; } = 5;
    public bool AutoPublicationEnabled { get; set; }
    public List<MentorDaypartWindowOptions>? Dayparts { get; set; }

    public MentorSlotPolicy ToPolicy()
    {
        if (Dayparts is null)
            return new MentorSlotPolicy(dailyBookingCap: DailyBookingCap, autoPublicationEnabled: AutoPublicationEnabled);

        var windows = Dayparts.Select(item => new MentorDaypartWindow(
            item.Daypart,
            ParseTime(item.StartLocal, nameof(item.StartLocal)),
            ParseTime(item.EndLocal, nameof(item.EndLocal))));

        return new MentorSlotPolicy(windows, DailyBookingCap, AutoPublicationEnabled);
    }

    private static TimeOnly ParseTime(string value, string fieldName)
    {
        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        throw new ArgumentException($"Trường {fieldName} phải dùng định dạng giờ 24 giờ HH:mm.");
    }
}

public sealed class MentorDaypartWindowOptions
{
    public MentorDaypart Daypart { get; set; }
    public string StartLocal { get; set; } = string.Empty;
    public string EndLocal { get; set; } = string.Empty;
}
