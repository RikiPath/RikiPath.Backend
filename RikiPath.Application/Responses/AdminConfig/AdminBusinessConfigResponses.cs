namespace RikiPath.Application.Responses.AdminConfig;

public sealed record AdminFeatureItemDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    string Category,
    string AccessMode,
    bool IsCoreFree,
    string? CreditFeatureKey,
    IReadOnlyList<string> Routes,
    int SortOrder,
    int LinkedPlanCount,
    // Availability (ACTIVE / DISABLED) and access policy kept separately; AccessMode above stays the effective mode
    // (DISABLED while suspended) for older clients. AccessPolicy is null for a legacy suspension with no recorded policy.
    string Availability = "ACTIVE",
    string? AccessPolicy = null,
    string? StoredState = null);

public sealed record AdminEntitlementPreviewItemDto(
    int FeatureId,
    string Code,
    string Name,
    string Category,
    string AccessMode,
    bool IsCoreFree,
    bool Allowed,
    string AccessSource,
    string? Note,
    // AVAILABLE / PLAN_LOCKED / CREDIT_LOCKED / PLAN_AND_CREDIT_LOCKED / ADMIN_DISABLED (internal codes; UI shows Vietnamese).
    string State = "AVAILABLE",
    string? Description = null);

public sealed record AdminEntitlementPreviewResponse(
    int? PlanId,
    string? PlanName,
    string? PlanCategory,
    int DurationDays,
    int AiGradingQuota,
    IReadOnlyList<AdminEntitlementPreviewItemDto> Features);

public sealed record AdminSubscriptionPlanDto(
    int Id,
    string Name,
    string? Description,
    string PlanCategory,
    int DurationDays,
    decimal Price,
    int AiGradingQuota,
    int MeetingSessionCount,
    bool IsPopular,
    bool IsTrial,
    bool IsActive,
    int SortOrder,
    int Revision,
    int TotalPurchaseCount,
    int PaidPurchaseCount,
    IReadOnlyList<int> FeatureIds,
    DateTime? CreatedDate,
    DateTime? ModifiedDate);

public sealed record AdminSubscriptionPlansWorkspaceResponse(
    IReadOnlyList<AdminSubscriptionPlanDto> Plans,
    IReadOnlyList<AdminFeatureItemDto> Features);

public sealed record AdminPaymentSummaryDto(
    int TotalCount,
    int PaidCount,
    int PendingCount,
    int FailedOrCancelledCount,
    int ReviewCount,
    decimal TotalPaidAmount);

public sealed record AdminPaymentTransactionDto(
    string Key,
    string Stream,
    int RecordId,
    long OrderCode,
    int UserId,
    string LearnerName,
    string LearnerEmail,
    string ItemTitle,
    string ItemCategory,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? ExpiresAtUtc,
    string? StatusReason);

public sealed record AdminPaymentListResponse(
    IReadOnlyList<AdminPaymentTransactionDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    AdminPaymentSummaryDto Summary);

public sealed record AdminPaymentDetailDto(
    string Key,
    string Stream,
    int RecordId,
    long OrderCode,
    int UserId,
    string LearnerName,
    string LearnerEmail,
    string ItemTitle,
    string ItemCategory,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? ProviderPaymentReference,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? ExpiresAtUtc,
    string? StatusReason,
    int? CohortId,
    string? EnrollmentStatus,
    DateTime? EntitlementStartUtc,
    DateTime? EntitlementEndUtc,
    int? AiQuota,
    int? AiUsed,
    int? DurationDaysSnapshot,
    int? PlanRevisionSnapshot,
    string? OrderSnapshotJson);

public sealed record PayOsGatewayStatusDto(
    bool ClientIdConfigured,
    bool ApiKeyConfigured,
    bool ChecksumKeyConfigured,
    bool IsReady,
    bool WebhookSignatureVerificationEnabled,
    string WebhookEndpointPath,
    string? WebReturnUrl,
    string? WebCancelUrl,
    string? MobileLandingBaseUrl,
    IReadOnlyList<string> SupportedStreams);

public sealed record AdminAuditLogDto(
    long Id,
    int? ActorUserId,
    string ActorName,
    string EntityType,
    int EntityId,
    string Action,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    DateTime CreatedAtUtc);
