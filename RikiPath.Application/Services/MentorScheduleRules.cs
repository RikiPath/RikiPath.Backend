namespace RikiPath.Application.Services;

public static class MentorScheduleRules
{
    public static DateTime VietnamNow(DateTime utcNow) => utcNow.ToUniversalTime().AddHours(7);
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static int StartHour(string period) => period switch
    {
        "Morning" => 8, "Afternoon" => 13, "Evening" => 18,
        _ => throw new ArgumentException("Buổi phải là Morning, Afternoon hoặc Evening.")
    };
    public static bool CanRegister(DateOnly date, DateTime utcNow)
    {
        var local = VietnamNow(utcNow);
        var monday = Monday(DateOnly.FromDateTime(local));
        return date >= monday.AddDays(7) && date < monday.AddDays(14)
            && local < monday.AddDays(4).ToDateTime(new TimeOnly(18, 0));
    }
    public static DateTime SlotStart(DateOnly date, string period, int index)
    {
        if (index is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(index));
        return DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(StartHour(period) + index, 0)).AddHours(-7), DateTimeKind.Utc);
    }
}
