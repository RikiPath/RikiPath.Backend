using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

/// <summary>Supported learner roadmap levels and canonical skill mappings, ordered from foundational to advanced.</summary>
internal static class JlptCurriculum
{
    public static int Rank(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "N5" => 1,
        "N4" => 2,
        "N3" => 3,
        "N2" => 4,
        "N1" => 5,
        _ => 0,
    };

    /// <summary>Adaptive and standard study roadmap levels (N5-N2).</summary>
    public static bool IsSupportedStudyLevel(string? code) => Rank(code) is >= 1 and <= 4;

    public static bool IsValidSpan(CertificateLevel starting, CertificateLevel target)
        => Rank(starting.Code) > 0 && Rank(target.Code) >= Rank(starting.Code);

    public static string ResolveSkillCode(int? skillId, string? skillCode, string? skillName)
    {
        var normalizedCode = skillCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(normalizedCode))
        {
            return normalizedCode switch
            {
                "VOCABULARY" => "vocabulary",
                "KANJI" => "kanji",
                "GRAMMAR" => "grammar",
                "LISTENING" => "listening",
                "READING" => "reading",
                _ => normalizedCode.ToLowerInvariant(),
            };
        }

        if (skillId is >= 1 and <= 5)
        {
            return skillId.Value switch
            {
                1 => "vocabulary",
                2 => "kanji",
                3 => "grammar",
                4 => "listening",
                5 => "reading",
                _ => "vocabulary",
            };
        }

        var trimmedName = (skillName ?? string.Empty).Trim();
        if (trimmedName.Contains("kanji", StringComparison.OrdinalIgnoreCase)
            || trimmedName.Contains("hán tự", StringComparison.OrdinalIgnoreCase)
            || trimmedName.Contains("han tu", StringComparison.OrdinalIgnoreCase)
            || trimmedName.Contains("漢字", StringComparison.Ordinal))
        {
            return "kanji";
        }

        return JlptSkillAssessmentPolicy.Classify(skillName)?.ToString().ToLowerInvariant()
            ?? (skillId.HasValue ? $"skill-{skillId.Value}" : "vocabulary");
    }

    public static JlptStudySkill? ResolveStudyGroup(int? skillId, string? skillCode, string? skillName)
    {
        return ResolveSkillCode(skillId, skillCode, skillName) switch
        {
            "vocabulary" or "kanji" => JlptStudySkill.Vocabulary,
            "grammar" => JlptStudySkill.Grammar,
            "reading" => JlptStudySkill.Reading,
            "listening" => JlptStudySkill.Listening,
            _ => JlptSkillAssessmentPolicy.Classify(skillName),
        };
    }

    public static string SkillLabel(string skillCode) => skillCode switch
    {
        "vocabulary" => "Từ vựng",
        "kanji" => "Hán tự (Kanji)",
        "grammar" => "Ngữ pháp",
        "reading" => "Đọc hiểu",
        "listening" => "Nghe hiểu",
        _ => skillCode,
    };
}
