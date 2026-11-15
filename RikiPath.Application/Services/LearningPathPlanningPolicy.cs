namespace RikiPath.Application.Services;

/// <summary>Transparent planning heuristics, not a JLPT pass predictor.</summary>
public static class LearningPathPlanningPolicy
{
    public const int RestDays = 3;

    public static string ClassifyHorizon(int largestSkillGap, int recommendedStudyDays, bool hasCompleteEvidence)
        => !hasCompleteEvidence || largestSkillGap >= 2 || recommendedStudyDays > 45 ? "long_term" : "short_term";

    public static int EstimateStudyDays(int gapLevels, double scorePercent, bool hasData, int dailyStudyMinutes)
    {
        // Baseline at 60 minutes/day. Even at the lowest level, a weak score needs remediation.
        var baselineDays = gapLevels switch { <= 0 => 14, 1 => 60, 2 => 120, _ => 180 + (gapLevels - 3) * 60 };
        var remediation = !hasData ? 60 : scorePercent switch { < 40 => 60, < 60 => 45, < 80 => 30, _ => 14 };
        return (int)Math.Ceiling(Math.Max(baselineDays, remediation) * 60d / Math.Clamp(dailyStudyMinutes, 15, 480));
    }

    public static int AvailableStudyDays(DateTime today, DateTime examDate)
        => Math.Max(0, (examDate.Date - today.Date).Days - RestDays);

    public static DateTime SuggestedExamDate(DateTime today, int studyDays)
    {
        var earliest = today.Date.AddDays(studyDays + RestDays);
        if (BusinessDateHelper.IsJlptExamMonth(earliest)) return earliest;
        return new DateTime(earliest.Year, earliest.Month < 7 ? 7 : 12, 1);
    }

    public static (int Min, int Max) ResolveDailyMinutes(int? fixedMinutes, int? min, int? max,
        int savedMinutes, int? savedMin, int? savedMax)
    {
        if (fixedMinutes.HasValue && (min.HasValue || max.HasValue))
            throw new ArgumentException("Chọn số phút cố định hoặc khoảng thời gian, không gửi đồng thời cả hai.");
        if (min.HasValue != max.HasValue)
            throw new ArgumentException("Khoảng thời gian cần có cả số phút tối thiểu và tối đa.");
        var minimum = fixedMinutes ?? min ?? savedMin ?? savedMinutes;
        var maximum = fixedMinutes ?? max ?? (min.HasValue ? min : savedMax) ?? minimum;
        if (minimum < 15 || maximum > 480 || minimum > maximum)
            throw new ArgumentException("Thời gian ôn luyện phải từ 15 đến 480 phút/ngày; tối thiểu không lớn hơn tối đa.");
        return (minimum, maximum);
    }
}
