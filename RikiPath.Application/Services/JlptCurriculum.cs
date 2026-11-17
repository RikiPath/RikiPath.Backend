using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

/// <summary>Supported learner roadmap levels, ordered from foundational to advanced.</summary>
internal static class JlptCurriculum
{
    public static int Rank(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "N5" => 1,
        "N4" => 2,
        "N3" => 3,
        "N2" => 4,
        _ => 0,
    };

    /// <summary>Standard study roadmap levels (N5-N3). N2 is supported only as a target goal.</summary>
    public static bool IsSupportedStudyLevel(string? code) => Rank(code) is >= 1 and <= 3;

    public static bool IsValidSpan(CertificateLevel starting, CertificateLevel target)
        => Rank(starting.Code) > 0 && Rank(target.Code) >= Rank(starting.Code);
}
