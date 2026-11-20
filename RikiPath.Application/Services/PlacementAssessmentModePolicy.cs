namespace RikiPath.Application.Services;

internal static class PlacementAssessmentModePolicy
{
    public const string NoCertificate = "NONE";
    public const int MinimumQuestionsPerSkillAndLevel = 3;

    public static string? ComparisonLevel(string? targetCode) => targetCode switch
    {
        "N1" => "N2",
        "N2" => "N3",
        "N3" => "N4",
        "N4" => "N5",
        _ => null,
    };

    private static bool SupportsDeclaration(string? targetCode) => targetCode is "N4" or "N3" or "N2" or "N1";

    public static bool IsValidDeclaration(string? targetCode, string? declaration)
    {
        if (!SupportsDeclaration(targetCode)) return false;
        if (declaration == NoCertificate) return true;

        var rank = JlptCurriculum.Rank(declaration);
        return rank > 0 && rank < JlptCurriculum.Rank(targetCode);
    }

    public static int ShortAssessmentQuestionsPerSkillAndLevel(string targetCode, string? declaration)
    {
        if (declaration is null or NoCertificate || !IsValidDeclaration(targetCode, declaration)) return 0;
        return JlptCurriculum.Rank(targetCode) - JlptCurriculum.Rank(declaration) == 3 ? 4 : 3;
    }

}
