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

public sealed record AdminPaymentQuery(
    string? Status = null,
    string? Stream = null,
    string? Search = null,
    string? FromDate = null,
    string? ToDate = null,
    int Page = 1,
    int PageSize = 20);
