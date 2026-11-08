namespace RikiPath.Domain.MentorCompensation;

/// <summary>
/// Tính bản chụp thù lao cho một buổi tư vấn 90 phút. Giá trị được chốt tại thời điểm đặt lịch.
/// </summary>
public static class MentorCompensationPolicy
{
    public const int SessionMinutes = 90;
    public const int FormulaVersion = 1;
    public const int CurrentFormulaVersion = FormulaVersion;
    public static readonly IReadOnlyList<string> SupportedJlptLevels = ["N5", "N4", "N3", "N2"];

    public static bool TryNormalizeSupportedLevel(string? jlptLevel, out string normalizedLevel)
    {
        normalizedLevel = string.Empty;
        if (string.IsNullOrWhiteSpace(jlptLevel))
            return false;

        var candidate = jlptLevel.Trim().ToUpperInvariant();
        if (candidate is not ("N5" or "N4" or "N3" or "N2"))
            return false;

        normalizedLevel = candidate;
        return true;
    }

    public static int ResolveDefaultCertificateLevelId(string normalizedLevel) => normalizedLevel switch
    {
        "N5" => 1,
        "N4" => 2,
        "N3" => 3,
        "N2" => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(normalizedLevel), "Chỉ hỗ trợ cấp độ JLPT từ N5 đến N2.")
    };

    public static string? ResolveDefaultLevelCode(int? certificateLevelId) => certificateLevelId switch
    {
        1 => "N5",
        2 => "N4",
        3 => "N3",
        4 => "N2",
        _ => null
    };

    public static MentorCompensationSnapshot CreateSnapshot(
        string jlptLevel,
        decimal hourlyRate,
        decimal levelCoefficient,
        string currency = "VND")
    {
        if (jlptLevel is not ("N5" or "N4" or "N3" or "N2"))
            throw new ArgumentOutOfRangeException(nameof(jlptLevel), "Chỉ hỗ trợ cấp độ JLPT từ N5 đến N2.");
        if (hourlyRate <= 0 || levelCoefficient <= 0)
            throw new ArgumentOutOfRangeException(nameof(hourlyRate), "Đơn giá và hệ số thù lao phải lớn hơn 0.");
        if (currency != "VND")
            throw new ArgumentException("Hiện chỉ hỗ trợ đồng Việt Nam.", nameof(currency));

        var amount = CalculateEligibleEarning(hourlyRate, levelCoefficient, SessionMinutes, currency);
        return new MentorCompensationSnapshot(jlptLevel, hourlyRate, levelCoefficient,
            SessionMinutes, amount, currency, FormulaVersion);
    }

    public static decimal CalculateEligibleEarning(
        decimal frozenHourlyRate,
        decimal frozenLevelCoefficient,
        int approvedEligibleMinutes,
        string currency = "VND")
    {
        if (frozenHourlyRate <= 0 || frozenLevelCoefficient <= 0)
            throw new ArgumentOutOfRangeException(nameof(frozenHourlyRate), "Đơn giá và hệ số thù lao phải lớn hơn 0.");
        if (approvedEligibleMinutes <= 0 || approvedEligibleMinutes > SessionMinutes)
            throw new ArgumentOutOfRangeException(nameof(approvedEligibleMinutes), $"Số phút được duyệt phải nằm trong khoảng 1 đến {SessionMinutes} phút.");
        if (currency != "VND")
            throw new ArgumentException("Hiện chỉ hỗ trợ đồng Việt Nam.", nameof(currency));

        return decimal.Round(frozenHourlyRate * frozenLevelCoefficient * approvedEligibleMinutes / 60m, 0,
            MidpointRounding.AwayFromZero);
    }
}

/// <summary>Bản chụp bất biến; thay đổi đơn giá trong tương lai không làm đổi buổi đã chốt.</summary>
public sealed record MentorCompensationSnapshot(
    string JlptLevel,
    decimal HourlyRate,
    decimal LevelCoefficient,
    int SessionMinutes,
    decimal ScheduledAmount,
    string Currency,
    int FormulaVersion);
