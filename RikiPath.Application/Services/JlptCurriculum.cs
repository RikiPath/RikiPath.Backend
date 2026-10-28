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
        _ => 0,
    };

    public static bool IsValidSpan(CertificateLevel starting, CertificateLevel target)
        => Rank(starting.Code) > 0 && Rank(target.Code) >= Rank(starting.Code);
}
