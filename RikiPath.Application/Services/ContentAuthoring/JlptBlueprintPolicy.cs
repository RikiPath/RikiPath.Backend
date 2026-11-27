using System.Net;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>
/// Chính sách cấu trúc đề thi JLPT chính thức (phiên bản JLPT-2026-V1) và cổng kiểm tra bản quyền tập trung.
/// Nguồn tham chiếu: https://www.jlpt.jp/e/guideline/testsections.html
/// </summary>
public static class JlptBlueprintPolicy
{
    public const string BlueprintVersion = "JLPT-2026-V1";
    public const string EffectiveDate = "2026-01-01";
    public const string OfficialGuidelineUrl = "https://www.jlpt.jp/e/guideline/testsections.html";

    public const string RightsVerifiedAuthorized = "VERIFIED_AUTHORIZED";
    public const string RightsReviewPending = "REVIEW_PENDING";
    public const string RightsRestricted = "RESTRICTED";
    public const string RightsUnknown = "UNKNOWN";

    private static readonly HashSet<string> AuthorizedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        RightsVerifiedAuthorized,
        "Owned",
        "Licensed",
        "PublicDomain",
        "Public_Domain",
        "Original",
        "Verified",
        "OriginalAuthor",
        "Original_Author",
        "RikiPathOriginal",
        "RikiPath_Original",
    };

    private static readonly HashSet<string> BlockedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        RightsReviewPending,
        RightsRestricted,
        RightsUnknown,
        "Pending",
        "PendingReview",
        "Pending_Review",
        "Unverified",
        "Blocked",
        "CopyrightUnknown",
        "Copyright_Unknown",
        "Quarantined",
    };

    public sealed record SectionBlueprintSpec(
        int Order,
        string Code,
        string Title,
        int DurationMinutes,
        IReadOnlyList<string> CoveredSkills);

    public sealed record LevelBlueprintSpec(
        string LevelCode,
        int TotalDurationMinutes,
        IReadOnlyList<SectionBlueprintSpec> TimedSections,
        bool HasWordUsageType,
        bool HasListeningVerbalExpressions);

    public static IReadOnlyDictionary<string, LevelBlueprintSpec> Levels { get; } =
        new Dictionary<string, LevelBlueprintSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["N5"] = new LevelBlueprintSpec(
                "N5",
                90,
                [
                    new(1, "N5_VOCAB", "Kiến thức ngôn ngữ (Từ vựng)", 20, ["Vocabulary", "Kanji"]),
                    new(2, "N5_GRAMMAR", "Kiến thức ngôn ngữ (Ngữ pháp)", 20, ["Grammar"]),
                    new(3, "N5_READING", "Đọc hiểu", 20, ["Reading"]),
                    new(4, "N5_LISTENING", "Nghe hiểu", 30, ["Listening"]),
                ],
                HasWordUsageType: false,
                HasListeningVerbalExpressions: true),
            ["N4"] = new LevelBlueprintSpec(
                "N4",
                115,
                [
                    new(1, "N4_VOCAB", "Kiến thức ngôn ngữ (Từ vựng)", 25, ["Vocabulary", "Kanji"]),
                    new(2, "N4_GRAMMAR_READING", "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu", 55, ["Grammar", "Reading"]),
                    new(3, "N4_LISTENING", "Nghe hiểu", 35, ["Listening"]),
                ],
                HasWordUsageType: true,
                HasListeningVerbalExpressions: true),
            ["N3"] = new LevelBlueprintSpec(
                "N3",
                140,
                [
                    new(1, "N3_VOCAB", "Kiến thức ngôn ngữ (Từ vựng)", 30, ["Vocabulary", "Kanji"]),
                    new(2, "N3_GRAMMAR_READING", "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu", 70, ["Grammar", "Reading"]),
                    new(3, "N3_LISTENING", "Nghe hiểu", 40, ["Listening"]),
                ],
                HasWordUsageType: true,
                HasListeningVerbalExpressions: true),
            ["N2"] = new LevelBlueprintSpec(
                "N2",
                155,
                [
                    new(1, "N2_KNOWLEDGE_READING", "Kiến thức ngôn ngữ (Từ vựng · Ngữ pháp) & Đọc hiểu", 105, ["Vocabulary", "Kanji", "Grammar", "Reading"]),
                    new(2, "N2_LISTENING", "Nghe hiểu", 50, ["Listening"]),
                ],
                HasWordUsageType: true,
                HasListeningVerbalExpressions: false),
        };

    public static LevelBlueprintSpec? TryGetBlueprint(string? levelCode) =>
        string.IsNullOrWhiteSpace(levelCode) ? null : Levels.GetValueOrDefault(levelCode.Trim());

    public static string NormalizeRightsStatus(string? rightsStatus)
    {
        if (string.IsNullOrWhiteSpace(rightsStatus))
            return RightsUnknown;

        var trimmed = rightsStatus.Trim();
        if (AuthorizedValues.Contains(trimmed))
            return trimmed.Equals(RightsVerifiedAuthorized, StringComparison.OrdinalIgnoreCase)
                ? RightsVerifiedAuthorized
                : trimmed;

        if (BlockedValues.Contains(trimmed))
        {
            if (trimmed.Contains("RESTRICTED", StringComparison.OrdinalIgnoreCase)) return RightsRestricted;
            if (trimmed.Contains("PENDING", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("REVIEW", StringComparison.OrdinalIgnoreCase)) return RightsReviewPending;
            return RightsUnknown;
        }

        return trimmed;
    }

    public static bool IsRightsAuthorized(string? rightsStatus) =>
        !string.IsNullOrWhiteSpace(rightsStatus) && AuthorizedValues.Contains(rightsStatus.Trim());

    public static bool IsRightsBlocked(string? rightsStatus)
    {
        if (string.IsNullOrWhiteSpace(rightsStatus))
            return true;

        var trimmed = rightsStatus.Trim();
        return BlockedValues.Contains(trimmed) || !AuthorizedValues.Contains(trimmed);
    }

    public static bool IsRightsBlockedForLegacyOrManaged(string? rightsStatus, bool isManagedWorkflow)
    {
        if (string.IsNullOrWhiteSpace(rightsStatus))
            return isManagedWorkflow;

        return IsRightsBlocked(rightsStatus);
    }

    public static (bool Blocked, HttpStatusCode StatusCode, string? Message) CheckPublishRights(params string?[] statuses)
    {
        foreach (var status in statuses)
        {
            if (IsRightsBlocked(status))
            {
                var normalized = NormalizeRightsStatus(status);
                return (
                    true,
                    HttpStatusCode.BadRequest,
                    $"Không thể xuất bản: nguồn nội dung có trạng thái bản quyền '{normalized}' chưa được xác minh quyền phát hành (PUBLISH_BLOCKED_RIGHTS)."
                );
            }
        }

        return (false, HttpStatusCode.OK, null);
    }
}
