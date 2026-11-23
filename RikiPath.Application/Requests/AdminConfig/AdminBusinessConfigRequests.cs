namespace RikiPath.Application.Requests.AdminConfig;

public sealed record SaveSubscriptionPlanRequest(
    string Name,
    string? Description,
    string PlanCategory,
    int DurationDays,
    decimal Price,
    int AiGradingQuota,
    bool IsPopular,
    bool IsActive,
    int SortOrder,
    int? ExpectedRevision = null,
    IReadOnlyList<int>? FeatureIds = null,
    string? Reason = null);

/// <summary>
/// Feature policy update. <c>AccessMode</c> is the access policy (FREE / PLAN_REQUIRED / CREDIT_REQUIRED / PLAN_AND_CREDIT);
/// <c>Availability</c> (ACTIVE / DISABLED) is independent. Legacy clients may still send <c>AccessMode = "DISABLED"</c>
/// without <c>Availability</c>: that suspends the feature and keeps its current policy.
/// <c>ExpectedState</c> is the stored state the Admin edited (optimistic concurrency); <c>ConfirmImpact</c> is required
/// for sensitive changes (suspending a feature, or any change to a core-free feature).
/// </summary>
public sealed record UpdateFeaturePolicyRequest(
    string AccessMode,
    string? Name = null,
    string? Description = null,
    string? CreditFeatureKey = null,
    string? Reason = null,
    string? Availability = null,
    string? ExpectedState = null,
    bool ConfirmImpact = false);

public sealed record AdminPaymentQuery(
    string? Status = null,
    string? Stream = null,
    string? Search = null,
    string? FromDate = null,
    string? ToDate = null,
    int Page = 1,
    int PageSize = 20);
