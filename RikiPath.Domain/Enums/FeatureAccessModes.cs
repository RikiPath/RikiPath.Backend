namespace RikiPath.Domain.Enums;

/// <summary>
/// Canonical access policy modes for RikiPath features.
/// <para>
/// Two independent dimensions share the single <c>Feature.AccessMode</c> column (no schema change):
/// <b>availability</b> (active / suspended by an Admin) and <b>access policy</b> (FREE, PLAN_REQUIRED, CREDIT_REQUIRED,
/// PLAN_AND_CREDIT). A suspended feature is stored as <c>DISABLED|&lt;policy&gt;</c> so re-opening restores the policy it
/// had; a bare legacy <c>DISABLED</c> means "suspended, policy not recorded". <see cref="Normalize"/> keeps returning
/// <see cref="Disabled"/> for any suspended value, so every existing reader keeps treating it as disabled.
/// </para>
/// </summary>
public static class FeatureAccessModes
{
    public const string Free = "FREE";
    public const string PlanRequired = "PLAN_REQUIRED";
    public const string CreditRequired = "CREDIT_REQUIRED";
    public const string PlanAndCredit = "PLAN_AND_CREDIT";
    public const string Disabled = "DISABLED";

    public const string AvailabilityActive = "ACTIVE";
    public const string AvailabilityDisabled = "DISABLED";
    private const char SuspendedSeparator = '|';

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Free,
        PlanRequired,
        CreditRequired,
        PlanAndCredit,
        Disabled,
    };

    /// <summary>Access policies (what a learner needs) — availability is a separate dimension.</summary>
    public static readonly IReadOnlySet<string> Policies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Free,
        PlanRequired,
        CreditRequired,
        PlanAndCredit,
    };

    /// <summary>Effective mode for evaluation: <see cref="Disabled"/> whenever the feature is suspended.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Free;
        var normalized = value.Trim().ToUpperInvariant();
        return IsSuspended(normalized) ? Disabled : normalized;
    }

    /// <summary>Whether an Admin has suspended the feature (independent of who could otherwise use it).</summary>
    public static bool IsSuspended(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return false;
        var normalized = stored.Trim().ToUpperInvariant();
        return normalized == Disabled || normalized.StartsWith(Disabled + SuspendedSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// The access policy kept underneath the availability flag. <c>null</c> for a legacy bare <c>DISABLED</c> row
    /// whose previous policy was never recorded (the Admin must choose one when re-opening it).
    /// </summary>
    public static string? PolicyOf(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Free;
        var normalized = stored.Trim().ToUpperInvariant();
        if (normalized == Disabled) return null;
        if (normalized.StartsWith(Disabled + SuspendedSeparator, StringComparison.Ordinal))
        {
            var policy = normalized[(Disabled.Length + 1)..];
            return Policies.Contains(policy) ? policy : null;
        }
        return Policies.Contains(normalized) ? normalized : null;
    }

    /// <summary>Stored value for an availability + policy pair (max 24 chars; column allows 32).</summary>
    public static string Compose(string policy, bool suspended)
    {
        var normalizedPolicy = policy.Trim().ToUpperInvariant();
        if (!Policies.Contains(normalizedPolicy))
            throw new ArgumentOutOfRangeException(nameof(policy), "Unknown feature access policy.");
        return suspended ? $"{Disabled}{SuspendedSeparator}{normalizedPolicy}" : normalizedPolicy;
    }
}

/// <summary>Stable canonical feature codes evaluated by the centralized entitlement engine.</summary>
public static class FeatureCodes
{
    public const string LessonsContent = "LESSONS_CONTENT";
    public const string JapaneseLibrary = "JAPANESE_LIBRARY";
    public const string PracticeExercises = "PRACTICE_EXERCISES";
    public const string MockTests = "MOCK_TESTS";
    public const string StandardRoadmap = "STANDARD_ROADMAP";
    public const string AiMockExplanation = "AI_MOCK_EXPLANATION";
    public const string MentorOneOnOneLegacy = "MENTOR_1ON1_LEGACY";
    public const string MentorRoomLegacy = "MENTOR_ROOM_LEGACY";
    public const string PersonalNotebook = "PERSONAL_NOTEBOOK";
    public const string SrsStudy = "SRS_STUDY";
    public const string PlacementTest = "PLACEMENT_TEST";
    public const string AiLearningPath = "AI_LEARNING_PATH";
    public const string AiSpeaking = "AI_SPEAKING";
    public const string AiWriting = "AI_WRITING";
}
