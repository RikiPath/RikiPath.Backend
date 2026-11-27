using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class SubscriptionAccessService(
    IUnitOfWork unitOfWork, IClaimService claims, IPaymentGatewayClient payments, AppSettings settings,
    ILogger<SubscriptionAccessService>? logger = null)
{
    public const int SpeakingPlan = 8101;
    public const int WritingPlan = 8102;
    public const int GradingCredit = 8103;
    public const int LearningPathCredit = 8104;
    public const int OrderPrefix = 900_000_000;

    public const string CategorySelfStudy = "self_study";
    public const string CategorySpeaking = "speaking";
    public const string CategoryWriting = "writing";
    public const string CategoryAiGrading = "ai_grading";
    public const string CategoryLearningPath = "learning_path";
    public const string CategoryCombo = "combo";
    public const string CategoryMentorLegacy = "mentor_legacy";

    public static readonly IReadOnlySet<string> SellableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CategorySelfStudy,
        CategorySpeaking,
        CategoryWriting,
        CategoryAiGrading,
        CategoryLearningPath,
        CategoryCombo,
    };

    public static bool IsUnlimitedExpiry(DateTime? endDate)
        => !endDate.HasValue || endDate.Value.Year >= 2099;

    public static string ResolveCategory(SubscriptionPlan? plan, int planId)
    {
        if (plan is not null)
        {
            if (plan.MeetingSessionCount > 0 || string.Equals(plan.PlanCategory, CategoryMentorLegacy, StringComparison.OrdinalIgnoreCase))
                return CategoryMentorLegacy;

            var normalized = plan.PlanCategory?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(normalized) && SellableCategories.Contains(normalized))
            {
                if (normalized == CategorySelfStudy && planId is SpeakingPlan or WritingPlan or GradingCredit or LearningPathCredit)
                {
                    return planId switch
                    {
                        SpeakingPlan => CategorySpeaking,
                        WritingPlan => CategoryWriting,
                        GradingCredit => CategoryAiGrading,
                        LearningPathCredit => CategoryLearningPath,
                        _ => CategorySelfStudy,
                    };
                }
                return normalized;
            }
        }

        return planId switch
        {
            SpeakingPlan => CategorySpeaking,
            WritingPlan => CategoryWriting,
            GradingCredit => CategoryAiGrading,
            LearningPathCredit => CategoryLearningPath,
            7 or 8105 => CategoryMentorLegacy,
            _ => CategorySelfStudy,
        };
    }

    public static bool IsCreditCategory(string? category)
        => string.Equals(category, CategoryAiGrading, StringComparison.OrdinalIgnoreCase)
        || string.Equals(category, CategoryLearningPath, StringComparison.OrdinalIgnoreCase);

    public static bool IsCredit(int planId) => planId is GradingCredit or LearningPathCredit;

    public static bool IsSellable(SubscriptionPlan? plan)
        => plan is not null
           && plan.IsActive
           && !plan.IsDeleted
           && plan.MeetingSessionCount == 0
           && ResolveCategory(plan, plan.Id) != CategoryMentorLegacy;

    private static string OrderCategory(UserSubscription order)
        => !string.IsNullOrWhiteSpace(order.PlanCategorySnapshot)
            ? order.PlanCategorySnapshot.Trim().ToLowerInvariant()
            : ResolveCategory(order.SubscriptionPlan, order.SubscriptionPlanId);

    public async Task<IReadOnlyList<StudyPlanOffer>> GetPlansAsync(CancellationToken ct)
    {
        var candidates = await unitOfWork.SubscriptionPlans.Query().AsNoTracking()
            .Include(p => p.Features)
            .Where(p => p.IsActive && !p.IsDeleted && p.MeetingSessionCount == 0 && p.PlanCategory != CategoryMentorLegacy)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        return candidates
            .Where(IsSellable)
            .Select(p => new StudyPlanOffer(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.DurationDays,
                p.AiGradingQuota,
                p.IsPopular,
                ResolveCategory(p, p.Id),
                p.Features
                    .Where(f => !f.IsDeleted && !string.Equals(f.Category, CategoryMentorLegacy, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.SortOrder)
                    .ThenBy(f => f.Id)
                    .Select(f => new StudyPlanFeatureDto(
                        f.Id,
                        f.Code,
                        f.Name,
                        f.Description,
                        f.Category,
                        FeatureAccessModes.Normalize(f.AccessMode)))
                    .ToList(),
                p.SortOrder))
            .ToList();
    }

    public async Task<StudyAccessResponse> GetAccessAsync(CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        await ReconcileRecentPendingAsync(userId, ct);
        var purchases = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Include(s => s.SubscriptionPlan)
                .ThenInclude(p => p!.Features)
            .Where(s => s.UserId == userId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Paid)
            .OrderByDescending(s => s.Id).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var active = purchases.Where(s => IsActive(s, now)).ToList();
        var study = active.Where(s => OrderCategory(s) is CategorySelfStudy or CategoryCombo)
            .OrderByDescending(s => IsUnlimitedExpiry(s.EndDate)).ThenByDescending(s => s.EndDate).FirstOrDefault();
        var studyExpiresAt = study?.EndDate is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : (DateTime?)null;
        int? studyDaysRemaining = study is null
            ? 0
            : IsUnlimitedExpiry(studyExpiresAt)
                ? null
                : (int)Math.Ceiling((studyExpiresAt!.Value - now).TotalDays);
        var grading = active.Where(s => OrderCategory(s) is CategoryAiGrading or CategorySelfStudy or CategoryCombo)
            .Sum(RemainingAi);
        var speaking = active.Any(s => OrderCategory(s) is CategorySpeaking or CategoryCombo) || study is not null || grading > 0;
        var writing = active.Any(s => OrderCategory(s) is CategoryWriting or CategoryCombo) || study is not null || grading > 0;
        var allFeatures = await unitOfWork.Features.Query().AsNoTracking()
            .Where(f => !f.IsDeleted && f.Category != CategoryMentorLegacy)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .ToListAsync(ct);
        var grants = ToGrantSnapshots(active);
        var evaluatedFeatures = allFeatures.Select(f => EvaluateFeaturePolicy(f, grants)).ToList();

        return new StudyAccessResponse(
            study is not null,
            study is null ? null : (study.PlanNameSnapshot ?? study.SubscriptionPlan?.Name),
            speaking,
            writing,
            grading,
            active.Where(s => OrderCategory(s) == CategorySpeaking).Sum(RemainingAi),
            active.Where(s => OrderCategory(s) == CategoryWriting).Sum(RemainingAi),
            active.Where(s => OrderCategory(s) == CategoryLearningPath).Sum(RemainingAi),
            0,
            purchases.Select(s => ToPurchasedStudyPlan(s, now)).ToList(),
            studyExpiresAt,
            studyDaysRemaining,
            evaluatedFeatures);
    }

    public async Task<PagedSubscriptionHistoryResponse> GetPurchaseHistoryAsync(
        string? status,
        string? category,
        int? days,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        await ReconcileRecentPendingAsync(userId, ct);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var now = DateTime.UtcNow;

        var query = unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Include(s => s.SubscriptionPlan)
                .ThenInclude(p => p!.Features)
            .Where(s => s.UserId == userId
                && !s.IsDeleted
                && s.MeetingSessionsIncluded == 0
                && (s.PlanCategorySnapshot == null || s.PlanCategorySnapshot != CategoryMentorLegacy));

        if (days is > 0)
        {
            var cutoff = now.AddDays(-days.Value);
            query = query.Where(s => s.PurchasedAt >= cutoff);
        }

        var allOrders = (await query
            .OrderByDescending(s => s.PurchasedAt)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct))
            .Where(s => OrderCategory(s) != CategoryMentorLegacy)
            .ToList();

        var availableCategories = allOrders
            .Select(OrderCategory)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IEnumerable<UserSubscription> filtered = allOrders;

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            var targetCategory = category.Trim().ToLowerInvariant();
            filtered = filtered.Where(s => string.Equals(OrderCategory(s), targetCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var trimmedStatus = status.Trim();
            if (string.Equals(trimmedStatus, "active", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(s => s.PaymentStatus == PaymentStatus.Paid && IsActive(s, now));
            else if (string.Equals(trimmedStatus, "ended", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(trimmedStatus, "plan_expired", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(s => s.PaymentStatus == PaymentStatus.Paid && !IsActive(s, now));
            else if (Enum.TryParse<PaymentStatus>(trimmedStatus, ignoreCase: true, out var parsedStatus))
                filtered = filtered.Where(s => s.PaymentStatus == parsedStatus);
        }

        var matched = filtered.ToList();
        var total = matched.Count;
        var items = matched
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(s => ToPurchasedStudyPlan(s, now))
            .ToList();

        return new PagedSubscriptionHistoryResponse(
            items,
            total,
            safePage,
            safePageSize,
            safePage * safePageSize < total,
            availableCategories);
    }

    private static PurchasedStudyPlan ToPurchasedStudyPlan(UserSubscription s, DateTime now)
    {
        var grantedFeatures = s.SubscriptionPlan?.Features?
            .Where(f => !f.IsDeleted && !string.Equals(f.Category, CategoryMentorLegacy, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new StudyPlanFeatureDto(
                f.Id,
                f.Code,
                f.Name,
                f.Description,
                f.Category,
                FeatureAccessModes.Normalize(f.AccessMode)))
            .ToList();

        return new PurchasedStudyPlan(
            s.Id,
            s.SubscriptionPlanId,
            s.PlanNameSnapshot ?? s.SubscriptionPlan?.Name ?? $"Gói #{s.SubscriptionPlanId}",
            s.AmountPaid,
            s.StartDate,
            s.EndDate,
            s.PaymentStatus == PaymentStatus.Paid && IsActive(s, now),
            RemainingAi(s),
            s.PurchasedAt,
            OrderCategory(s),
            s.PaymentStatus.ToString(),
            s.DurationDaysSnapshot ?? s.SubscriptionPlan?.DurationDays,
            s.AiGradingQuota,
            !string.IsNullOrWhiteSpace(s.PaymentTransactionId) ? s.PaymentTransactionId : $"RP-{s.Id:D6}",
            s.AmountPaid <= 0 ? "Miễn phí" : "PayOS",
            grantedFeatures);
    }

    public async Task<FeatureAccessDecision> EvaluateFeatureAsync(string featureCode, CancellationToken ct)
    {
        var feature = await unitOfWork.Features.Query().AsNoTracking()
            .FirstOrDefaultAsync(f => !f.IsDeleted && f.Code == featureCode, ct);
        if (feature is null)
            return new FeatureAccessDecision(true, featureCode, FeatureAccessModes.Free, "FREE", null, null, HttpStatusCode.OK);

        // Admin suspension is checked first and independently of plans: a suspended feature is unavailable to every
        // learner (including core-free tools and paying learners) until it is re-opened.
        var mode = EffectiveMode(feature);
        if (mode == FeatureAccessModes.Disabled)
            return new FeatureAccessDecision(false, feature.Code, mode, "DISABLED",
                "FEATURE_DISABLED", SuspendedMessage, HttpStatusCode.Forbidden);
        if (mode == FeatureAccessModes.Free)
            return new FeatureAccessDecision(true, feature.Code, mode, "FREE", null, null, HttpStatusCode.OK);

        var userId = claims.GetUserClaim().Id;
        var now = DateTime.UtcNow;
        var active = (await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Include(s => s.SubscriptionPlan)
                .ThenInclude(p => p!.Features)
            .Where(s => s.UserId == userId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Paid)
            .ToListAsync(ct))
            .Where(s => IsActive(s, now))
            .ToList();

        var evaluated = EvaluateFeaturePolicy(feature, ToGrantSnapshots(active));
        if (evaluated.Allowed)
            return new FeatureAccessDecision(true, evaluated.Code, evaluated.AccessMode, evaluated.AccessSource, null, null, HttpStatusCode.OK);

        var errCode = evaluated.AccessSource == "LOCKED" && evaluated.Code == FeatureCodes.LessonsContent && mode == FeatureAccessModes.PlanRequired
            ? "SELF_STUDY_PACKAGE_REQUIRED"
            : evaluated.RequiresCredit && evaluated.CreditsRemaining <= 0 &&
              (mode == FeatureAccessModes.CreditRequired || (mode == FeatureAccessModes.PlanAndCredit && evaluated.GrantedByPlanName is not null))
                ? "AI_CREDIT_REQUIRED"
                : "FEATURE_PLAN_REQUIRED";
        return new FeatureAccessDecision(false, evaluated.Code, evaluated.AccessMode, evaluated.AccessSource,
            errCode, evaluated.LockReason ?? "Bạn cần gói tự học đang hiệu lực.", HttpStatusCode.Forbidden);
    }

    internal static IReadOnlyList<string> ParseFeatureRoutes(string? routePrefixesJson)
    {
        if (string.IsNullOrWhiteSpace(routePrefixesJson)) return [];
        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(routePrefixesJson);
            return parsed is { Length: > 0 }
                ? parsed.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal sealed record SubscriptionGrantSnapshot(
        string? PlanName,
        string Category,
        int AiQuotaRemaining,
        IReadOnlySet<int> FeatureIds);

    private static List<SubscriptionGrantSnapshot> ToGrantSnapshots(IReadOnlyList<UserSubscription> activeOrders)
        => activeOrders.Select(order => new SubscriptionGrantSnapshot(
            order.PlanNameSnapshot ?? order.SubscriptionPlan?.Name,
            OrderCategory(order),
            RemainingAi(order),
            order.SubscriptionPlan?.Features?
                .Where(f => !f.IsDeleted)
                .Select(f => f.Id)
                .ToHashSet() ?? (IReadOnlySet<int>)new HashSet<int>()))
            .ToList();

    internal static FeatureEntitlementDto EvaluateFeaturePolicy(
        Feature feature,
        IReadOnlyList<SubscriptionGrantSnapshot> activeGrants)
    {
        var routes = ParseFeatureRoutes(feature.RoutePrefixesJson);
        var mode = EffectiveMode(feature);

        // Suspension wins over everything (core-free, purchased plans, credits).
        if (mode == FeatureAccessModes.Disabled)
        {
            return new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                mode, feature.IsCoreFree, false, "DISABLED", null, false, 0,
                SuspendedMessage, null, routes);
        }

        if (mode == FeatureAccessModes.Free)
        {
            return new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                mode, feature.IsCoreFree, true, "FREE", null, false, 0, null, null, routes);
        }

        var matchingPlan = activeGrants.FirstOrDefault(grant =>
            grant.FeatureIds.Contains(feature.Id) ||
            (feature.Code == FeatureCodes.LessonsContent && grant.Category is CategorySelfStudy or CategoryCombo));

        var creditKey = feature.CreditFeatureKey?.Trim();
        var creditsRemaining = string.IsNullOrWhiteSpace(creditKey)
            ? 0
            : SaturatingSum(activeGrants
                .Where(grant => SupportsCredit(grant.Category, creditKey))
                .Select(grant => grant.AiQuotaRemaining));

        return mode switch
        {
            FeatureAccessModes.PlanRequired => new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                mode, false, matchingPlan is not null, matchingPlan is not null ? "PLAN" : "LOCKED",
                matchingPlan?.PlanName, false, 0,
                matchingPlan is not null
                    ? null
                    : (feature.Code == FeatureCodes.LessonsContent
                        ? "Bạn cần gói tự học đang hiệu lực."
                        : $"Tính năng {feature.Name} yêu cầu gói học phù hợp."),
                matchingPlan is not null ? null : "/plans/",
                routes),

            FeatureAccessModes.CreditRequired => new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                mode, false, creditsRemaining > 0, creditsRemaining > 0 ? "CREDIT" : "LOCKED",
                null, true, creditsRemaining,
                creditsRemaining > 0 ? null : "Bạn chưa có lượt AI cho tính năng này. Vào Mua gói để mua thêm lượt.",
                creditsRemaining > 0 ? null : "/plans/",
                routes),

            FeatureAccessModes.PlanAndCredit => new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                mode, false, matchingPlan is not null && creditsRemaining > 0,
                matchingPlan is not null && creditsRemaining > 0 ? "PLAN_AND_CREDIT" : "LOCKED",
                matchingPlan?.PlanName, true, creditsRemaining,
                matchingPlan is null
                    ? $"Tính năng {feature.Name} yêu cầu gói học phù hợp và lượt AI."
                    : (creditsRemaining > 0 ? null : "Bạn đã hết lượt AI khả dụng cho tính năng này."),
                matchingPlan is not null && creditsRemaining > 0 ? null : "/plans/",
                routes),

            _ => new FeatureEntitlementDto(
                feature.Id, feature.Code, feature.Name, feature.Description, feature.Category,
                FeatureAccessModes.Free, feature.IsCoreFree, true, "FREE", null, false, 0, null, null, routes),
        };
    }

    public const string SuspendedMessage = "Tính năng này đang tạm ngừng cung cấp. Bạn có thể quay lại sử dụng khi hệ thống mở lại.";

    /// <summary>
    /// The one mode the engine evaluates: DISABLED while an Admin suspends the feature, otherwise its stored access
    /// policy. `IsCoreFree` is only the seed's original classification; every feature's policy is Admin-configurable.
    /// </summary>
    internal static string EffectiveMode(Feature feature)
        => FeatureAccessModes.IsSuspended(feature.AccessMode)
            ? FeatureAccessModes.Disabled
            : FeatureAccessModes.Normalize(feature.AccessMode);

    /// <summary>
    /// Effective feature state for one learner/plan scenario (internal codes; UIs translate them):
    /// AVAILABLE · ADMIN_DISABLED · CREDIT_LOCKED · PLAN_AND_CREDIT_LOCKED · PLAN_LOCKED.
    /// </summary>
    public static string EffectiveState(FeatureEntitlementDto evaluated)
    {
        if (evaluated.Allowed) return "AVAILABLE";
        if (evaluated.AccessSource == "DISABLED") return "ADMIN_DISABLED";
        return evaluated.AccessMode switch
        {
            FeatureAccessModes.CreditRequired => "CREDIT_LOCKED",
            FeatureAccessModes.PlanAndCredit => evaluated.GrantedByPlanName is not null ? "CREDIT_LOCKED" : "PLAN_AND_CREDIT_LOCKED",
            _ => "PLAN_LOCKED",
        };
    }

    private static bool SupportsCredit(string category, string feature) => feature switch
    {
        "learning_path" => category is CategoryLearningPath or CategoryCombo,
        "speaking" => category is CategorySpeaking or CategoryAiGrading or CategorySelfStudy or CategoryCombo,
        "writing" => category is CategoryWriting or CategoryAiGrading or CategorySelfStudy or CategoryCombo,
        MockTestExplanationService.CreditFeature => category is CategoryAiGrading or CategorySelfStudy or CategoryCombo,
        _ => false
    };

    /// <summary>PayOS hosted-checkout link lifetime for a self-study order; the order is held Pending for the same window.</summary>
    public static readonly TimeSpan PaymentLinkLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LinkCreationGrace = TimeSpan.FromSeconds(45);
    private const int MaxOrderCodeAttempts = 3;

    public async Task<ApiResponse<StudyCheckout>> PurchaseAsync(int planId, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var plan = await unitOfWork.SubscriptionPlans.GetByIdAsync(planId, ct);
        if (!IsSellable(plan))
            return ApiResponse<StudyCheckout>.Fail("Gói học không còn mở bán.", HttpStatusCode.NotFound, "PLAN_NOT_AVAILABLE");
        if (plan!.Price <= 0 || plan.Price != decimal.Truncate(plan.Price) || plan.Price > int.MaxValue)
        {
            logger?.LogError("Study plan has a price PayOS cannot charge. PlanId={PlanId}", plan.Id);
            return ApiResponse<StudyCheckout>.Fail("Gói học đang được cập nhật giá. Vui lòng thử lại sau.", HttpStatusCode.Conflict, "PLAN_PRICE_INVALID");
        }
        var returnBase = settings.PayOs?.WebReturnUrl;
        var cancelBase = settings.PayOs?.WebCancelUrl;
        if (!IsWebUrl(returnBase) || !IsWebUrl(cancelBase))
        {
            logger?.LogError("PayOS return/cancel URLs are not configured; study checkout disabled.");
            return ApiResponse<StudyCheckout>.Fail("Cổng thanh toán tạm thời chưa sẵn sàng. Vui lòng thử lại sau.",
                HttpStatusCode.ServiceUnavailable, "PAYMENT_GATEWAY_NOT_CONFIGURED");
        }

        ApiResponse<StudyCheckout>? response = null;
        // One checkout at a time per learner: the account row lock serialises double clicks, parallel tabs and retries,
        // so a learner never ends up with several payable links for the same plan.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            response = null;
            await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
            var now = DateTime.UtcNow;

            var pending = await unitOfWork.UserSubscriptions.Query()
                .Where(s => s.UserId == userId && !s.IsDeleted && s.SubscriptionPlanId == plan.Id
                    && s.PaymentStatus == PaymentStatus.Pending)
                .OrderByDescending(s => s.Id)
                .ToListAsync(token);

            foreach (var open in pending)
            {
                var reusable = StudyOrderCodes.TryReadPendingReference(open.PaymentTransactionId, open.Id, out _, out _)
                    && open.PaymentExpiresAt is { } expires && expires > now.AddMinutes(1)
                    && open.AmountPaid == plan.Price
                    && open.PlanRevisionSnapshot == plan.Revision;
                if (reusable)
                {
                    // Resume the still-valid link instead of opening a second order for the same plan.
                    StudyOrderCodes.TryReadPendingReference(open.PaymentTransactionId, open.Id, out var openLinkId, out _);
                    response = ApiResponse<StudyCheckout>.Success(new(open.Id, CheckoutUrlFor(openLinkId),
                        DateTime.SpecifyKind(open.PaymentExpiresAt!.Value, DateTimeKind.Utc), true));
                    return;
                }
                if (string.IsNullOrWhiteSpace(open.PaymentTransactionId) && open.PurchasedAt > now - LinkCreationGrace)
                {
                    response = ApiResponse<StudyCheckout>.Fail("Đơn hàng đang được tạo. Vui lòng đợi trong giây lát.",
                        HttpStatusCode.Conflict, "PAYMENT_LINK_IN_PROGRESS", retryable: true);
                    return;
                }
            }

            var order = new UserSubscription
            {
                UserId = userId,
                SubscriptionPlanId = plan.Id,
                AmountPaid = plan.Price,
                PurchasedAt = now,
                PaymentExpiresAt = now + PaymentLinkLifetime,
                AiGradingQuota = plan.AiGradingQuota,
                DurationDaysSnapshot = plan.DurationDays,
                PlanNameSnapshot = plan.Name,
                PlanCategorySnapshot = ResolveCategory(plan, plan.Id),
                PlanRevisionSnapshot = plan.Revision,
            };
            await unitOfWork.UserSubscriptions.AddAsync(order, token);
            await unitOfWork.SaveChangesAsync(token);

            var orderId = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var returnUrl = QueryHelpers.AddQueryString(returnBase!, "purchaseId", orderId);
            var cancelUrl = QueryHelpers.AddQueryString(cancelBase!, "purchaseId", orderId);
            // ≤ 9 ký tự để chuyển khoản được với mọi ngân hàng (giới hạn PayOS cho tài khoản không liên kết).
            var description = order.Id <= 99_999_999 ? $"RP{order.Id}" : $"RP{order.Id % 10_000_000}";
            PaymentGatewayException? lastFailure = null;
            for (var attempt = 1; attempt <= MaxOrderCodeAttempts; attempt++)
            {
                var orderCode = StudyOrderCodes.Create(order.Id);
                try
                {
                    var link = await payments.CreatePaymentLinkAsync(orderCode, checked((int)order.AmountPaid), description,
                        returnUrl, cancelUrl, order.PaymentExpiresAt, token);
                    order.PaymentTransactionId = StudyOrderCodes.PendingReference(link.PaymentLinkId, orderCode);
                    await unitOfWork.SaveChangesAsync(token);
                    logger?.LogInformation("Study checkout created. PurchaseId={PurchaseId} PlanId={PlanId} OrderCode={OrderCode} Attempt={Attempt}",
                        order.Id, plan.Id, orderCode, attempt);
                    response = ApiResponse<StudyCheckout>.Success(new(order.Id, link.CheckoutUrl,
                        DateTime.SpecifyKind(order.PaymentExpiresAt!.Value, DateTimeKind.Utc), false));
                    return;
                }
                catch (PaymentGatewayException ex) when (ex.Failure == PaymentGatewayFailure.DuplicateOrderCode)
                {
                    // Another environment sharing this PayOS channel already used the code: draw a new one.
                    lastFailure = ex;
                    logger?.LogWarning("PayOS order code already used; retrying with a new code. PurchaseId={PurchaseId} OrderCode={OrderCode} Attempt={Attempt}",
                        order.Id, orderCode, attempt);
                }
                catch (PaymentGatewayException ex)
                {
                    lastFailure = ex;
                    break;
                }
            }

            // No usable link reached the learner, so the order can never be paid: close it instead of leaving it Pending.
            order.PaymentStatus = PaymentStatus.Failed;
            await unitOfWork.SaveChangesAsync(token);
            logger?.LogWarning("Study checkout failed. PurchaseId={PurchaseId} PlanId={PlanId} Failure={Failure} HttpStatus={HttpStatus} ProviderCode={ProviderCode} ProviderDesc={ProviderDesc}",
                order.Id, plan.Id, lastFailure?.Failure, lastFailure?.HttpStatus, lastFailure?.ProviderCode, lastFailure?.ProviderMessage);
            response = CheckoutFailure(lastFailure);
        }, ct);

        return response ?? ApiResponse<StudyCheckout>.Fail("Không tạo được đơn mua gói.", HttpStatusCode.InternalServerError);
    }

    private static ApiResponse<StudyCheckout> CheckoutFailure(PaymentGatewayException? failure) => failure?.Failure switch
    {
        PaymentGatewayFailure.NotConfigured or PaymentGatewayFailure.AuthenticationFailed => ApiResponse<StudyCheckout>.Fail(
            "Cổng thanh toán tạm thời chưa sẵn sàng. Vui lòng thử lại sau.", HttpStatusCode.ServiceUnavailable, "PAYMENT_GATEWAY_UNAVAILABLE", retryable: false),
        PaymentGatewayFailure.RateLimited => ApiResponse<StudyCheckout>.Fail(
            "PayOS đang nhận quá nhiều yêu cầu. Vui lòng thử lại sau ít phút.", HttpStatusCode.TooManyRequests, "PAYMENT_GATEWAY_RATE_LIMITED", retryable: true),
        PaymentGatewayFailure.Timeout or PaymentGatewayFailure.Network or PaymentGatewayFailure.ProviderUnavailable => ApiResponse<StudyCheckout>.Fail(
            "PayOS đang phản hồi chậm. Vui lòng thử lại.", HttpStatusCode.GatewayTimeout, "PAYMENT_GATEWAY_TIMEOUT", retryable: true),
        _ => ApiResponse<StudyCheckout>.Fail(
            "PayOS chưa tạo được liên kết thanh toán. Vui lòng thử lại.", HttpStatusCode.BadGateway, "PAYMENT_GATEWAY_REJECTED", retryable: true),
    };

    /// <summary>PayOS hosted-checkout address of a payment link (the same URL PayOS returns as <c>checkoutUrl</c>).</summary>
    internal static string CheckoutUrlFor(string paymentLinkId) => $"https://pay.payos.vn/web/{Uri.EscapeDataString(paymentLinkId)}";

    public async Task<ApiResponse<string>> GetPurchaseStatusAsync(int id, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var order = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted && s.UserId == userId, ct);
        if (order is null) return ApiResponse<string>.NotFound("Không tìm thấy đơn mua gói.");
        if (order.PaymentStatus != PaymentStatus.Pending)
            return ApiResponse<string>.Success(order.PaymentStatus.ToString());

        var status = await ReconcileAsync(userId, order, ct);
        return ApiResponse<string>.Success(status.ToString());
    }

    /// <summary>
    /// Server-to-server reconciliation of one Pending order with PayOS (covers a lost/late webhook, a webhook that hit a
    /// database error, a closed tab or a new device). Only an authenticated provider lookup whose link id, amount and order
    /// code all match the stored order can change it; a provider outage leaves it Pending for the next attempt.
    /// </summary>
    private async Task<PaymentStatus> ReconcileAsync(int userId, UserSubscription order, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(order.PaymentTransactionId))
        {
            // The link was never created (or its creation outcome was lost); after the grace period the order is unpayable.
            return order.PurchasedAt <= now - LinkCreationGrace
                ? await TransitionAsync(userId, order.Id, null, PaymentStatus.Failed, ct)
                : PaymentStatus.Pending;
        }

        if (!StudyOrderCodes.TryReadPendingReference(order.PaymentTransactionId, order.Id, out var linkId, out var boundOrderCode))
        {
            logger?.LogError("Pending study order has an unreadable payment reference; left for manual review. PurchaseId={PurchaseId}", order.Id);
            return PaymentStatus.Pending;
        }

        PaymentLinkInformation remote;
        try
        {
            remote = await payments.GetPaymentInformationAsync(linkId, ct);
        }
        catch (PaymentGatewayException ex)
        {
            logger?.LogWarning("PayOS reconciliation lookup failed; order stays Pending. PurchaseId={PurchaseId} Failure={Failure} ProviderCode={ProviderCode}",
                order.Id, ex.Failure, ex.ProviderCode);
            return PaymentStatus.Pending;
        }

        var matches = string.Equals(remote.PaymentLinkId, linkId, StringComparison.Ordinal)
            && remote.Amount == order.AmountPaid
            && remote.OrderCode == boundOrderCode;
        if (!matches)
        {
            logger?.LogError("PayOS reconciliation mismatch; order left untouched. PurchaseId={PurchaseId} RemoteOrderCode={OrderCode}",
                order.Id, remote.OrderCode);
            return PaymentStatus.Pending;
        }

        PaymentStatus? target = remote.Status.Trim().ToUpperInvariant() switch
        {
            "PAID" when remote.AmountPaid >= order.AmountPaid => PaymentStatus.Paid,
            "CANCELLED" when remote.AmountPaid == 0 => PaymentStatus.Cancelled,
            "EXPIRED" when remote.AmountPaid == 0 => PaymentStatus.Expired,
            _ => null,
        };
        if (target is null) return PaymentStatus.Pending;
        return await TransitionAsync(userId, order.Id, target == PaymentStatus.Paid ? remote.PaymentLinkId : null, target.Value, ct);
    }

    /// <summary>Moves a Pending order to its final state under the learner lock; a concurrent webhook/poll that already did it wins.</summary>
    private async Task<PaymentStatus> TransitionAsync(int userId, int orderId, string? reference, PaymentStatus target, CancellationToken ct)
    {
        var result = PaymentStatus.Pending;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                var current = await unitOfWork.UserSubscriptions.GetWithPlanAsync(orderId, token);
                if (current is null) return;
                if (current.PaymentStatus == PaymentStatus.Pending)
                {
                    if (target == PaymentStatus.Paid)
                    {
                        var stackFrom = await FindActiveCategoryExpiryAsync(
                            unitOfWork, userId, OrderCategory(current), current.Id, DateTime.UtcNow, token);
                        Activate(current, reference, stackFrom);
                        logger?.LogInformation("Study plan activated by reconciliation. PurchaseId={PurchaseId} PlanId={PlanId}",
                            current.Id, current.SubscriptionPlanId);
                    }
                    else current.PaymentStatus = target;
                    await unitOfWork.SaveChangesAsync(token);
                }
                result = current.PaymentStatus;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A webhook updated the same order first; the next poll reads its final state.
        }
        return result;
    }

    /// <summary>Reconciles the learner's recent Pending orders so entitlements appear on any device even if no webhook arrived.</summary>
    private async Task ReconcileRecentPendingAsync(int userId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-2);
        var open = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Pending
                && s.MeetingSessionsIncluded == 0 && s.PurchasedAt > cutoff)
            .OrderByDescending(s => s.Id)
            .Take(3)
            .ToListAsync(ct);
        foreach (var order in open)
            await ReconcileAsync(userId, order, ct);
    }

    internal static async Task<DateTime?> FindActiveCategoryExpiryAsync(
        IUnitOfWork unitOfWork, int userId, string category, int excludeOrderId, DateTime now, CancellationToken ct)
    {
        if (IsCreditCategory(category)) return null;
        var activeExpiries = await unitOfWork.UserSubscriptions.Query()
            .AsNoTracking()
            .Include(s => s.SubscriptionPlan)
            .Where(s => s.UserId == userId
                && s.Id != excludeOrderId
                && !s.IsDeleted
                && s.PaymentStatus == PaymentStatus.Paid
                && s.EndDate != null
                && s.EndDate > now
                && s.EndDate.Value.Year < 2099)
            .ToListAsync(ct);

        return activeExpiries
            .Where(s => string.Equals(OrderCategory(s), category, StringComparison.OrdinalIgnoreCase))
            .Max(s => s.EndDate);
    }

    internal static string OrderCategoryPublic(UserSubscription order) => OrderCategory(order);

    internal static void Activate(UserSubscription order, string? reference, DateTime? stackFromUtc = null)
    {
        var now = DateTime.UtcNow;
        var category = OrderCategory(order);
        var durationDays = order.DurationDaysSnapshot ?? order.SubscriptionPlan?.DurationDays ?? 0;
        order.PaymentStatus = PaymentStatus.Paid;
        order.PaymentTransactionId = reference;
        order.StartDate = now;
        if (IsCreditCategory(category) || IsCredit(order.SubscriptionPlanId))
        {
            order.EndDate = null;
        }
        else
        {
            var effectiveStart = stackFromUtc.HasValue && stackFromUtc.Value > now ? stackFromUtc.Value : now;
            order.EndDate = effectiveStart.AddDays(durationDays);
        }
        order.AiGradingUsedCount = 0;
    }

    public async Task<ApiResponse<T>> UseAiAsync<T>(string feature, Func<Task<ApiResponse<T>>> operation, CancellationToken ct,
        Func<T?, bool>? delivered = null)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role == Role.Mentor && feature is "speaking" or "writing")
            return await operation();

        var userId = actor.Id;
        int? reservationId = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token) is null) return;
            var now = DateTime.UtcNow;
            var orders = await unitOfWork.UserSubscriptions.Query()
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.UserId == userId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Paid &&
                    s.AiGradingUsedCount < s.AiGradingQuota)
                .OrderBy(s => s.EndDate == null).ThenBy(s => s.EndDate).ThenBy(s => s.Id).ToListAsync(token);
            var credit = orders.FirstOrDefault(s => IsActive(s, now) && Supports(s, feature));
            if (credit is null) return;
            credit.AiGradingUsedCount++;
            await unitOfWork.SaveChangesAsync(token);
            reservationId = credit.Id;
        }, ct);

        if (reservationId is null)
            return ApiResponse<T>.Fail("Bạn chưa có lượt AI cho tính năng này. Vào Mua gói để mua thêm lượt.",
                HttpStatusCode.Forbidden, errorCode: "AI_CREDIT_REQUIRED");

        var succeeded = false;
        try
        {
            var result = await operation();
            succeeded = result.IsSuccess && (delivered is null || delivered(result.Result));
            if (succeeded)
                logger?.LogInformation("AI credit consumed. UserId={UserId} Feature={Feature} PurchaseId={PurchaseId} Credits={Credits}",
                    userId, feature, reservationId.Value, 1);
            return result;
        }
        finally
        {
            if (!succeeded)
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    // An atomic update avoids overwriting usage from another request.
                    await unitOfWork.UserSubscriptions.Query()
                        .Where(s => s.Id == reservationId.Value && s.AiGradingUsedCount > 0)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.AiGradingUsedCount,
                            s => s.AiGradingUsedCount - 1), token);
                }, CancellationToken.None);
        }
    }

    private static bool Supports(UserSubscription order, string feature)
        => SupportsCredit(OrderCategory(order), feature);


    private static bool IsActive(UserSubscription order, DateTime now)
        => (order.StartDate is null || order.StartDate <= now) && (order.EndDate is null || order.EndDate > now);

    private static int RemainingAi(UserSubscription order) => Math.Max(0, order.AiGradingQuota - order.AiGradingUsedCount);

    // Several demo subscriptions can be active at once. Keep the public DTO
    // backward-compatible (int) while preventing LINQ Sum(int) overflow when
    // each demo subscription carries a very large quota.
    private static int SaturatingSumRemainingAi(IEnumerable<UserSubscription> orders)
        => SaturatingSum(orders.Select(RemainingAi));

    private static int SaturatingSum(IEnumerable<int> values)
    {
        long total = 0;
        foreach (var value in values)
        {
            total += Math.Max(0, value);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return (int)total;
    }
    private static bool IsWebUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed record StudyPlanFeatureDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    string Category,
    string AccessMode);

public sealed record FeatureEntitlementDto(
    int FeatureId,
    string Code,
    string Name,
    string? Description,
    string Category,
    string AccessMode,
    bool IsCoreFree,
    bool Allowed,
    string AccessSource,
    string? GrantedByPlanName,
    bool RequiresCredit,
    int CreditsRemaining,
    string? LockReason,
    string? UnlockPath,
    IReadOnlyList<string> Routes);

public sealed record FeatureAccessDecision(
    bool Allowed,
    string Code,
    string AccessMode,
    string AccessSource,
    string? ErrorCode,
    string? Message,
    HttpStatusCode StatusCode);

public sealed record StudyPlanOffer(int Id, string Name, string? Description, decimal Price, int DurationDays,
    int AiUses, bool IsPopular, string Kind, IReadOnlyList<StudyPlanFeatureDto>? Features = null, int SortOrder = 0);
public sealed record StudyCheckout(int PurchaseId, string CheckoutUrl, DateTime? ExpiresAt = null, bool Resumed = false);
public sealed record PurchasedStudyPlan(
    int Id,
    int PlanId,
    string PlanName,
    decimal AmountPaid,
    DateTime? StartDate,
    DateTime? EndDate,
    bool Active,
    int AiUsesRemaining,
    DateTime? PurchasedAt = null,
    string? Kind = null,
    string? PaymentStatus = null,
    int? DurationDays = null,
    int? AiUsesGranted = null,
    string? OrderCode = null,
    string? PaymentMethod = null,
    IReadOnlyList<StudyPlanFeatureDto>? GrantedFeatures = null);
public sealed record PagedSubscriptionHistoryResponse(
    IReadOnlyList<PurchasedStudyPlan> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore,
    IReadOnlyList<string> AvailableCategories);
public sealed record StudyAccessResponse(bool Active, string? PlanName, bool Speaking, bool Writing,
    int AiGradingUsesRemaining, int SpeakingUsesRemaining, int WritingUsesRemaining, int LearningPathUsesRemaining,
    int MentorSessionsRemaining, IReadOnlyList<PurchasedStudyPlan> Purchases,
    DateTime? SelfStudyExpiresAt, int? SelfStudyDaysRemaining,
    IReadOnlyList<FeatureEntitlementDto>? Features = null);
