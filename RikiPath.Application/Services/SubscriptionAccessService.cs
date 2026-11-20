using System.Net;
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
    public const string CategoryMentorLegacy = "mentor_legacy";

    public static readonly IReadOnlySet<string> SellableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CategorySelfStudy,
        CategorySpeaking,
        CategoryWriting,
        CategoryAiGrading,
        CategoryLearningPath,
    };

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
                ResolveCategory(p, p.Id)))
            .ToList();
    }

    public async Task<StudyAccessResponse> GetAccessAsync(CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var purchases = await unitOfWork.UserSubscriptions.Query().AsNoTracking().Include(s => s.SubscriptionPlan)
            .Where(s => s.UserId == userId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Paid)
            .OrderByDescending(s => s.Id).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var active = purchases.Where(s => IsActive(s, now)).ToList();
        var study = active.Where(s => OrderCategory(s) == CategorySelfStudy)
            .OrderByDescending(s => s.EndDate is null).ThenByDescending(s => s.EndDate).FirstOrDefault();
        var studyExpiresAt = study?.EndDate is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : (DateTime?)null;
        int? studyDaysRemaining = studyExpiresAt is { } expiry
            ? (int)Math.Ceiling((expiry - now).TotalDays)
            : study is null ? 0 : null;
        var grading = active.Where(s => OrderCategory(s) is CategoryAiGrading or CategorySelfStudy)
            .Sum(RemainingAi);
        var speaking = active.Any(s => OrderCategory(s) == CategorySpeaking) || study is not null || grading > 0;
        var writing = active.Any(s => OrderCategory(s) == CategoryWriting) || study is not null || grading > 0;
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
            purchases.Select(s => new PurchasedStudyPlan(
                s.Id,
                s.SubscriptionPlanId,
                s.PlanNameSnapshot ?? s.SubscriptionPlan?.Name ?? $"Gói #{s.SubscriptionPlanId}",
                s.AmountPaid,
                s.StartDate,
                s.EndDate,
                IsActive(s, now),
                RemainingAi(s))).ToList(),
            studyExpiresAt,
            studyDaysRemaining);
    }

    public async Task<ApiResponse<StudyCheckout>> PurchaseAsync(int planId, CancellationToken ct)
    {
        var plan = await unitOfWork.SubscriptionPlans.GetByIdAsync(planId, ct);
        if (!IsSellable(plan))
            return ApiResponse<StudyCheckout>.NotFound("Gói học không còn mở bán.");
        var returnBase = settings.PayOs?.WebReturnUrl;
        var cancelBase = settings.PayOs?.WebCancelUrl;
        if (!IsWebUrl(returnBase) || !IsWebUrl(cancelBase))
            return ApiResponse<StudyCheckout>.Fail("Chưa cấu hình URL thành công và hủy thanh toán PayOS.", HttpStatusCode.ServiceUnavailable);

        var order = new UserSubscription
        {
            UserId = claims.GetUserClaim().Id,
            SubscriptionPlanId = plan!.Id,
            AmountPaid = plan.Price,
            PurchasedAt = DateTime.UtcNow,
            AiGradingQuota = plan.AiGradingQuota,
            DurationDaysSnapshot = plan.DurationDays,
            PlanNameSnapshot = plan.Name,
            PlanCategorySnapshot = ResolveCategory(plan, plan.Id),
            PlanRevisionSnapshot = plan.Revision,
        };
        await unitOfWork.UserSubscriptions.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);
        try
        {
            var returnUrl = QueryHelpers.AddQueryString(returnBase!, "purchaseId", order.Id.ToString());
            var cancelUrl = QueryHelpers.AddQueryString(cancelBase!, "purchaseId", order.Id.ToString());
            var link = await payments.CreatePaymentLinkAsync(OrderPrefix + (long)order.Id, checked((int)order.AmountPaid),
                $"RikiPath {order.Id}", returnUrl, cancelUrl, ct);
            order.PaymentTransactionId = link.PaymentLinkId;
            await unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<StudyCheckout>.Success(new(order.Id, link.CheckoutUrl));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiResponse<StudyCheckout>.Fail("Không tạo được liên kết thanh toán PayOS.", HttpStatusCode.BadGateway);
        }
    }

    public async Task<ApiResponse<string>> GetPurchaseStatusAsync(int id, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var order = await unitOfWork.UserSubscriptions.Query().AsNoTracking().Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted && s.UserId == userId, ct);
        if (order is null) return ApiResponse<string>.NotFound("Không tìm thấy đơn mua gói.");
        if (order.PaymentStatus != PaymentStatus.Pending || string.IsNullOrWhiteSpace(order.PaymentTransactionId))
            return ApiResponse<string>.Success(order.PaymentStatus.ToString());

        try
        {
            var orderCode = OrderPrefix + (long)id;
            var remote = await payments.GetPaymentInformationAsync(orderCode, ct);
            var paid = remote.Status == "PAID" && remote.AmountPaid == order.AmountPaid;
            var cancelled = remote.Status == "CANCELLED" && remote.AmountPaid == 0;
            if ((paid || cancelled) && remote.OrderCode == orderCode &&
                remote.PaymentLinkId == order.PaymentTransactionId &&
                remote.Amount == order.AmountPaid)
            {
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    var current = await unitOfWork.UserSubscriptions.GetWithPlanAsync(id, token);
                    if (current is null) return;
                    if (current.PaymentStatus == PaymentStatus.Pending)
                    {
                        if (paid) Activate(current, remote.PaymentLinkId);
                        else current.PaymentStatus = PaymentStatus.Failed;
                        await unitOfWork.SaveChangesAsync(token);
                    }
                    order.PaymentStatus = current.PaymentStatus;
                }, ct);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            // A provider outage or concurrent webhook can be retried by the next status poll.
        }
        return ApiResponse<string>.Success(order.PaymentStatus.ToString());
    }

    internal static void Activate(UserSubscription order, string? reference)
    {
        var now = DateTime.UtcNow;
        var category = OrderCategory(order);
        var durationDays = order.DurationDaysSnapshot ?? order.SubscriptionPlan?.DurationDays ?? 0;
        order.PaymentStatus = PaymentStatus.Paid;
        order.PaymentTransactionId = reference;
        order.StartDate = now;
        order.EndDate = IsCreditCategory(category) || IsCredit(order.SubscriptionPlanId)
            ? null
            : now.AddDays(durationDays);
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
    {
        var category = OrderCategory(order);
        return feature switch
        {
            "learning_path" => category == CategoryLearningPath,
            "speaking" => category is CategorySpeaking or CategoryAiGrading or CategorySelfStudy,
            "writing" => category is CategoryWriting or CategoryAiGrading or CategorySelfStudy,
            _ => false
        };
    }

    private static bool IsActive(UserSubscription order, DateTime now)
        => (order.StartDate is null || order.StartDate <= now) && (order.EndDate is null || order.EndDate > now);

    private static int RemainingAi(UserSubscription order) => Math.Max(0, order.AiGradingQuota - order.AiGradingUsedCount);
    private static bool IsWebUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed record StudyPlanOffer(int Id, string Name, string? Description, decimal Price, int DurationDays,
    int AiUses, bool IsPopular, string Kind);
public sealed record StudyCheckout(int PurchaseId, string CheckoutUrl);
public sealed record PurchasedStudyPlan(int Id, int PlanId, string PlanName, decimal AmountPaid, DateTime? StartDate,
    DateTime? EndDate, bool Active, int AiUsesRemaining);
public sealed record StudyAccessResponse(bool Active, string? PlanName, bool Speaking, bool Writing,
    int AiGradingUsesRemaining, int SpeakingUsesRemaining, int WritingUsesRemaining, int LearningPathUsesRemaining,
    int MentorSessionsRemaining, IReadOnlyList<PurchasedStudyPlan> Purchases,
    DateTime? SelfStudyExpiresAt, int? SelfStudyDaysRemaining);
