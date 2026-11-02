using System.Globalization;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Options;

public sealed class MentorSlotGenerationOptions
{
    public const string SectionName = "MentorSlotGeneration";

    public int DailyBookingCap { get; set; } = 4;
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
