using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminConfig;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConfig;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class AdminBusinessConfigService(
    IUnitOfWork uow,
    AppSettings settings) : IAdminBusinessConfigService
{
    private const long CourseOrderPrefix = 700_000_000;
    private const long TopUpOrderPrefix = 800_000_000;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    private static string CollapseSpaces(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : MultiSpaceRegex().Replace(value.Trim(), " ");

    private static string? NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SanitizeReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > 300 ? trimmed[..300] : trimmed;
    }

    public async Task<ApiResponse<AdminSubscriptionPlansWorkspaceResponse>> GetSubscriptionPlansAsync(CancellationToken ct = default)
    {
        var plans = await uow.SubscriptionPlans.Query()
            .AsNoTracking()
            .Include(x => x.Features)
            .Where(x => !x.IsDeleted && x.MeetingSessionCount == 0 && x.PlanCategory != SubscriptionAccessService.CategoryMentorLegacy)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var stats = await uow.UserSubscriptions.Query()
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .GroupBy(s => s.SubscriptionPlanId)
            .Select(g => new
            {
                PlanId = g.Key,
                Total = g.Count(),
                Paid = g.Count(s => s.PaymentStatus == PaymentStatus.Paid),
            })
            .ToDictionaryAsync(x => x.PlanId, x => x, ct);

        var featureEntities = await uow.Features.Query()
            .AsNoTracking()
            .Include(f => f.SubscriptionPlans)
            .Where(f => !f.IsDeleted && f.Category != SubscriptionAccessService.CategoryMentorLegacy)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);

        var features = featureEntities
            .Select(ToFeatureDto)
            .ToList();

        var items = plans
            .Where(p => SubscriptionAccessService.ResolveCategory(p, p.Id) != SubscriptionAccessService.CategoryMentorLegacy)
            .Select(p =>
            {
                stats.TryGetValue(p.Id, out var count);
                return ToPlanDto(p, count?.Total ?? 0, count?.Paid ?? 0);
            })
            .ToList();

        return ApiResponse<AdminSubscriptionPlansWorkspaceResponse>.Success(
            new AdminSubscriptionPlansWorkspaceResponse(items, features));
    }

    public async Task<ApiResponse<AdminSubscriptionPlanDto>> SaveSubscriptionPlanAsync(
        int? id,
        SaveSubscriptionPlanRequest request,
        int adminUserId,
        CancellationToken ct = default)
    {
        var name = CollapseSpaces(request.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Tên gói học là bắt buộc và tối đa 150 ký tự.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_NAME_INVALID");

        var description = NormalizeDescription(request.Description);
        if ((description?.Length ?? 0) > 500)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Mô tả gói học tối đa 500 ký tự.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_DESCRIPTION_TOO_LONG");

        var category = request.PlanCategory?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SubscriptionAccessService.SellableCategories.Contains(category))
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Loại gói học không hợp lệ. Chỉ chấp nhận: self_study, speaking, writing, ai_grading, learning_path.",
                HttpStatusCode.BadRequest,
                "PLAN_CATEGORY_INVALID");

        if (request.Price < 0 || request.Price != decimal.Truncate(request.Price) || request.Price > int.MaxValue)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Giá gói phải là số nguyên VND không âm.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_PRICE_INVALID");

        if (request.IsActive && request.Price <= 0)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Gói đang mở bán phải có giá lớn hơn 0 VND.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_PRICE_REQUIRED");

        var isCredit = SubscriptionAccessService.IsCreditCategory(category);
        if (isCredit && request.DurationDays != 0)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Gói mua theo lượt AI phải có thời hạn bằng 0 ngày.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_DURATION_INVALID");

        if (!isCredit && request.DurationDays is < 1 or > 1095)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Thời hạn gói học phải từ 1 đến 1095 ngày.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_DURATION_INVALID");

        if (request.AiGradingQuota is < 0 or > 5000)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Số lượt AI phải từ 0 đến 5000.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_QUOTA_INVALID");

        if (category != SubscriptionAccessService.CategorySelfStudy && request.AiGradingQuota < 1)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Gói kỹ năng hoặc lượt AI cần tối thiểu 1 lượt sử dụng AI.",
                HttpStatusCode.BadRequest,
                "SUBSCRIPTION_PLAN_QUOTA_INVALID");

        if (request.SortOrder < 1)
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Thứ tự hiển thị phải là số nguyên dương từ 1.",
                HttpStatusCode.BadRequest,
                "DISPLAY_ORDER_INVALID");

        SubscriptionPlan? saved = null;
        int totalPurchases = 0;
        int paidPurchases = 0;
        ApiResponse<AdminSubscriptionPlanDto>? failure = null;

        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                var existingPlans = await uow.SubscriptionPlans.Query()
                    .Include(x => x.Features)
                    .Where(x => !x.IsDeleted)
                    .ToListAsync(token);

                if (existingPlans.Any(x => x.Id != id && string.Equals(CollapseSpaces(x.Name), name, StringComparison.OrdinalIgnoreCase)))
                {
                    failure = ApiResponse<AdminSubscriptionPlanDto>.Fail(
                        "Tên gói học đã tồn tại.",
                        HttpStatusCode.Conflict,
                        "SUBSCRIPTION_PLAN_DUPLICATE_NAME");
                    return;
                }

                SubscriptionPlan? existing = null;
                string? beforeJson = null;
                if (id.HasValue)
                {
                    existing = existingPlans.FirstOrDefault(x => x.Id == id.Value);
                    if (existing is null || existing.MeetingSessionCount > 0 ||
                        SubscriptionAccessService.ResolveCategory(existing, existing.Id) == SubscriptionAccessService.CategoryMentorLegacy)
                    {
                        failure = ApiResponse<AdminSubscriptionPlanDto>.NotFound("Không tìm thấy gói học hợp lệ.");
                        return;
                    }

                    if (request.ExpectedRevision.HasValue && existing.Revision != request.ExpectedRevision.Value)
                    {
                        failure = ApiResponse<AdminSubscriptionPlanDto>.Fail(
                            "Gói học vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                            HttpStatusCode.Conflict,
                            "ORDER_REVISION_CONFLICT");
                        return;
                    }

                    beforeJson = JsonSerializer.Serialize(new
                    {
                        existing.Id,
                        existing.Name,
                        existing.Description,
                        existing.PlanCategory,
                        existing.DurationDays,
                        existing.Price,
                        existing.AiGradingQuota,
                        existing.IsPopular,
                        existing.IsActive,
                        existing.SortOrder,
                        existing.Revision,
                        FeatureIds = existing.Features.Select(f => f.Id).OrderBy(x => x).ToArray(),
                    });
                }

                var selectedFeatures = new List<Feature>();
                if (request.FeatureIds is { Count: > 0 })
                {
                    var distinctIds = request.FeatureIds.Distinct().ToList();
                    selectedFeatures = await uow.Features.Query()
                        .Where(f => !f.IsDeleted && distinctIds.Contains(f.Id))
                        .ToListAsync(token);
                    if (selectedFeatures.Count != distinctIds.Count)
                    {
                        failure = ApiResponse<AdminSubscriptionPlanDto>.Fail(
                            "Có quyền lợi được chọn không tồn tại trong hệ thống.",
                            HttpStatusCode.BadRequest,
                            "FEATURE_NOT_FOUND");
                        return;
                    }
                }

                var now = DateTime.UtcNow;
                var entity = existing ?? new SubscriptionPlan
                {
                    CreatedDate = now,
                    Revision = 1,
                };

                entity.Name = name;
                entity.Description = description;
                entity.PlanCategory = category;
                entity.DurationDays = request.DurationDays;
                entity.Price = request.Price;
                entity.AiGradingQuota = request.AiGradingQuota;
                entity.IsPopular = request.IsPopular;
                entity.IsActive = request.IsActive;
                entity.SortOrder = request.SortOrder;
                entity.ModifiedDate = now;
                if (request.FeatureIds is not null)
                {
                    entity.Features.Clear();
                    foreach (var f in selectedFeatures)
                        entity.Features.Add(f);
                }

                if (existing is not null)
                {
                    entity.Revision++;
                }
                else
                {
                    await uow.SubscriptionPlans.AddAsync(entity, token);
                }

                await uow.SaveChangesAsync(token);

                var afterJson = JsonSerializer.Serialize(new
                {
                    entity.Id,
                    entity.Name,
                    entity.Description,
                    entity.PlanCategory,
                    entity.DurationDays,
                    entity.Price,
                    entity.AiGradingQuota,
                    entity.IsPopular,
                    entity.IsActive,
                    entity.SortOrder,
                    entity.Revision,
                    FeatureIds = entity.Features.Select(f => f.Id).OrderBy(x => x).ToArray(),
                });

                await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = adminUserId,
                    EntityType = "SubscriptionPlan",
                    EntityId = entity.Id,
                    Action = existing is null ? "Created" : PlanAuditAction(beforeJson, afterJson),
                    BeforeJson = beforeJson,
                    AfterJson = afterJson,
                    Reason = SanitizeReason(request.Reason),
                    CreatedAtUtc = now,
                }, token);
                await uow.SaveChangesAsync(token);

                if (existing is not null)
                {
                    var stats = await uow.UserSubscriptions.Query()
                        .AsNoTracking()
                        .Where(s => !s.IsDeleted && s.SubscriptionPlanId == entity.Id)
                        .GroupBy(_ => 1)
                        .Select(g => new
                        {
                            Total = g.Count(),
                            Paid = g.Count(s => s.PaymentStatus == PaymentStatus.Paid),
                        })
                        .FirstOrDefaultAsync(token);
                    totalPurchases = stats?.Total ?? 0;
                    paidPurchases = stats?.Paid ?? 0;
                }

                saved = entity;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<AdminSubscriptionPlanDto>.Fail(
                "Gói học vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                HttpStatusCode.Conflict,
                "ORDER_REVISION_CONFLICT");
        }

        if (failure is not null) return failure;
        return ApiResponse<AdminSubscriptionPlanDto>.Success(
            ToPlanDto(saved!, totalPurchases, paidPurchases),
            message: id.HasValue ? "Đã cập nhật gói học." : "Đã tạo gói học mới.");
    }

    public async Task<ApiResponse<AdminFeatureItemDto>> UpdateFeaturePolicyAsync(
        int featureId,
        UpdateFeaturePolicyRequest request,
        int adminUserId,
        CancellationToken ct = default)
    {
        var requestedMode = FeatureAccessModes.Normalize(request.AccessMode);
        var availability = string.IsNullOrWhiteSpace(request.Availability)
            ? null
            : request.Availability.Trim().ToUpperInvariant();
        if (availability is not null && availability is not (FeatureAccessModes.AvailabilityActive or FeatureAccessModes.AvailabilityDisabled))
            return ApiResponse<AdminFeatureItemDto>.Fail(
                "Trạng thái cung cấp không hợp lệ.",
                HttpStatusCode.BadRequest,
                "FEATURE_AVAILABILITY_INVALID");

        // Legacy contract: AccessMode = DISABLED (no Availability) means "suspend, keep the current policy".
        var legacySuspend = requestedMode == FeatureAccessModes.Disabled && availability is null;
        if (!legacySuspend && !FeatureAccessModes.Policies.Contains(requestedMode))
            return ApiResponse<AdminFeatureItemDto>.Fail(
                "Điều kiện truy cập không hợp lệ.",
                HttpStatusCode.BadRequest,
                "FEATURE_ACCESS_MODE_INVALID");

        if (request.Name is not null)
        {
            var trimmedName = CollapseSpaces(request.Name);
            if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 150)
                return ApiResponse<AdminFeatureItemDto>.Fail(
                    "Tên tính năng là bắt buộc và tối đa 150 ký tự.",
                    HttpStatusCode.BadRequest,
                    "FEATURE_NAME_INVALID");
        }

        if ((NormalizeDescription(request.Description)?.Length ?? 0) > 500)
            return ApiResponse<AdminFeatureItemDto>.Fail(
                "Mô tả tính năng tối đa 500 ký tự.",
                HttpStatusCode.BadRequest,
                "FEATURE_DESCRIPTION_TOO_LONG");

        AdminFeatureItemDto? updatedDto = null;
        ApiResponse<AdminFeatureItemDto>? failure = null;

        await uow.ExecuteInTransactionAsync(async token =>
        {
            failure = null;
            var feature = await uow.Features.Query()
                .Include(f => f.SubscriptionPlans)
                .FirstOrDefaultAsync(f => f.Id == featureId && !f.IsDeleted, token);
            if (feature is null)
            {
                failure = ApiResponse<AdminFeatureItemDto>.NotFound("Không tìm thấy tính năng.");
                return;
            }

            if (string.Equals(feature.Category, SubscriptionAccessService.CategoryMentorLegacy, StringComparison.OrdinalIgnoreCase))
            {
                failure = ApiResponse<AdminFeatureItemDto>.Fail(
                    "Không thể chỉnh sửa tính năng mentor cũ đã ngừng sử dụng.",
                    HttpStatusCode.BadRequest,
                    "FEATURE_LEGACY_READONLY");
                return;
            }

            var storedBefore = StoredStateOf(feature);
            // Optimistic concurrency: the Admin saved on top of the state they saw; a newer change is never overwritten.
            if (!string.IsNullOrWhiteSpace(request.ExpectedState) &&
                !string.Equals(request.ExpectedState.Trim(), storedBefore, StringComparison.OrdinalIgnoreCase))
            {
                failure = ApiResponse<AdminFeatureItemDto>.Fail(
                    "Tính năng vừa được quản trị viên khác cập nhật. Vui lòng tải lại trước khi lưu.",
                    HttpStatusCode.Conflict,
                    "ORDER_REVISION_CONFLICT");
                return;
            }

            var wasSuspended = FeatureAccessModes.IsSuspended(feature.AccessMode);
            var previousPolicy = FeatureAccessModes.PolicyOf(feature.AccessMode);
            // Without Availability, a legacy client sending a policy re-opens the feature (the previous single-field behaviour).
            var nextSuspended = availability is null ? legacySuspend : availability == FeatureAccessModes.AvailabilityDisabled;
            var nextPolicy = legacySuspend
                ? previousPolicy ?? (feature.IsCoreFree ? FeatureAccessModes.Free : FeatureAccessModes.PlanRequired)
                : requestedMode;

            var nextCreditKey = request.CreditFeatureKey is null
                ? feature.CreditFeatureKey
                : (string.IsNullOrWhiteSpace(request.CreditFeatureKey) ? null : request.CreditFeatureKey.Trim());
            if ((nextPolicy is FeatureAccessModes.CreditRequired or FeatureAccessModes.PlanAndCredit) &&
                string.IsNullOrWhiteSpace(nextCreditKey))
            {
                failure = ApiResponse<AdminFeatureItemDto>.Fail(
                    "Tính năng này chưa gắn với loại lượt AI nào nên chưa thể yêu cầu lượt AI.",
                    HttpStatusCode.BadRequest,
                    "FEATURE_CREDIT_KEY_REQUIRED");
                return;
            }

            var availabilityChanged = wasSuspended != nextSuspended;
            var policyChanged = !string.Equals(previousPolicy, nextPolicy, StringComparison.Ordinal);
            var reason = SanitizeReason(request.Reason);
            // Sensitive = learners lose access they have today: suspending, or moving a free feature behind a plan/credits.
            var sensitive = (availabilityChanged && nextSuspended)
                || (policyChanged && previousPolicy == FeatureAccessModes.Free && nextPolicy != FeatureAccessModes.Free);
            if (sensitive && (!request.ConfirmImpact || string.IsNullOrWhiteSpace(reason)))
            {
                failure = ApiResponse<AdminFeatureItemDto>.Fail(
                    "Thay đổi này ảnh hưởng tới học viên đang sử dụng. Vui lòng nhập lý do và xác nhận trước khi lưu.",
                    HttpStatusCode.BadRequest,
                    "FEATURE_CHANGE_CONFIRMATION_REQUIRED");
                return;
            }

            var beforeJson = JsonSerializer.Serialize(new
            {
                feature.Id,
                feature.Code,
                feature.Name,
                feature.Description,
                Availability = wasSuspended ? FeatureAccessModes.AvailabilityDisabled : FeatureAccessModes.AvailabilityActive,
                AccessPolicy = previousPolicy,
                feature.CreditFeatureKey,
                feature.IsCoreFree,
            });

            var now = DateTime.UtcNow;
            feature.AccessMode = FeatureAccessModes.Compose(nextPolicy, nextSuspended);
            feature.CreditFeatureKey = nextCreditKey;
            if (request.Name is not null)
                feature.Name = CollapseSpaces(request.Name);
            if (request.Description is not null)
                feature.Description = NormalizeDescription(request.Description);
            feature.ModifiedDate = now;

            var afterJson = JsonSerializer.Serialize(new
            {
                feature.Id,
                feature.Code,
                feature.Name,
                feature.Description,
                Availability = nextSuspended ? FeatureAccessModes.AvailabilityDisabled : FeatureAccessModes.AvailabilityActive,
                AccessPolicy = nextPolicy,
                feature.CreditFeatureKey,
                feature.IsCoreFree,
            });

            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = adminUserId,
                EntityType = "FeatureAccessPolicy",
                EntityId = feature.Id,
                Action = availabilityChanged
                    ? (nextSuspended ? "FeatureSuspended" : "FeatureReopened")
                    : policyChanged ? "AccessPolicyChanged" : "Updated",
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                Reason = reason,
                CreatedAtUtc = now,
            }, token);

            await uow.SaveChangesAsync(token);
            updatedDto = ToFeatureDto(feature);
        }, ct);

        if (failure is not null) return failure;
        return ApiResponse<AdminFeatureItemDto>.Success(updatedDto!, message: "Đã lưu cấu hình tính năng.");
    }

    public async Task<ApiResponse<AdminEntitlementPreviewResponse>> GetEntitlementPreviewAsync(
        int? planId,
        CancellationToken ct = default)
    {
        SubscriptionPlan? plan = null;
        var grants = new List<SubscriptionAccessService.SubscriptionGrantSnapshot>();

        if (planId.HasValue)
        {
            plan = await uow.SubscriptionPlans.Query()
                .AsNoTracking()
                .Include(p => p.Features)
                .FirstOrDefaultAsync(p => p.Id == planId.Value && !p.IsDeleted, ct);
            if (plan is null || plan.MeetingSessionCount > 0 ||
                SubscriptionAccessService.ResolveCategory(plan, plan.Id) == SubscriptionAccessService.CategoryMentorLegacy)
            {
                return ApiResponse<AdminEntitlementPreviewResponse>.NotFound("Không tìm thấy gói tự học hợp lệ.");
            }

            grants.Add(new SubscriptionAccessService.SubscriptionGrantSnapshot(
                plan.Name,
                SubscriptionAccessService.ResolveCategory(plan, plan.Id),
                plan.AiGradingQuota,
                plan.Features.Where(f => !f.IsDeleted).Select(f => f.Id).ToHashSet()));
        }

        var features = await uow.Features.Query()
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.Category != SubscriptionAccessService.CategoryMentorLegacy)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);

        var items = features.Select(f =>
        {
            var evaluated = SubscriptionAccessService.EvaluateFeaturePolicy(f, grants);
            var note = evaluated.Allowed
                ? evaluated.AccessSource switch
                {
                    "FREE" => "Miễn phí cho mọi học viên",
                    "PLAN" => $"Mở bởi gói {evaluated.GrantedByPlanName ?? "đã chọn"}",
                    "CREDIT" => $"Khả dụng {evaluated.CreditsRemaining} lượt AI",
                    "PLAN_AND_CREDIT" => $"Mở bởi gói + {evaluated.CreditsRemaining} lượt AI",
                    _ => null,
                }
                : evaluated.LockReason;

            return new AdminEntitlementPreviewItemDto(
                evaluated.FeatureId,
                evaluated.Code,
                evaluated.Name,
                evaluated.Category,
                evaluated.AccessMode,
                evaluated.IsCoreFree,
                evaluated.Allowed,
                evaluated.AccessSource,
                note,
                SubscriptionAccessService.EffectiveState(evaluated),
                f.Description);
        }).ToList();

        return ApiResponse<AdminEntitlementPreviewResponse>.Success(new AdminEntitlementPreviewResponse(
            plan?.Id,
            plan?.Name,
            plan is null ? "free" : SubscriptionAccessService.ResolveCategory(plan, plan.Id),
            plan?.DurationDays ?? 0,
            plan?.AiGradingQuota ?? 0,
            items));
    }

    public async Task<ApiResponse<AdminPaymentListResponse>> GetPaymentsAsync(AdminPaymentQuery query, CancellationToken ct = default)
    {
        var all = await LoadAllTransactionsAsync(ct);

        IEnumerable<AdminPaymentTransactionDto> filtered = all;

        var streamFilter = query.Stream?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(streamFilter) && streamFilter != "all")
            filtered = filtered.Where(x => string.Equals(x.Stream, streamFilter, StringComparison.OrdinalIgnoreCase));

        var fromUtc = ParseBoundaryDate(query.FromDate, isEnd: false);
        var toUtc = ParseBoundaryDate(query.ToDate, isEnd: true);
        if (fromUtc.HasValue)
            filtered = filtered.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            filtered = filtered.Where(x => x.CreatedAtUtc <= toUtc.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(x =>
                x.OrderCode.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.LearnerName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.LearnerEmail.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.ItemTitle.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(x.PaymentReference) && x.PaymentReference.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        // Summary counts reflect current stream/date/search scope before single-status tab filtering
        var scopeList = filtered.ToList();
        var summary = new AdminPaymentSummaryDto(
            scopeList.Count,
            scopeList.Count(x => x.Status == "Paid"),
            scopeList.Count(x => x.Status == "Pending"),
            scopeList.Count(x => x.Status is "Failed" or "Cancelled" or "Refunded"),
            scopeList.Count(x => x.Status == "PaymentReview"),
            scopeList.Where(x => x.Status == "Paid").Sum(x => x.Amount));

        var statusFilter = query.Status?.Trim();
        if (!string.IsNullOrEmpty(statusFilter) && !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = scopeList.Where(x => string.Equals(x.Status, statusFilter, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            filtered = scopeList;
        }

        var ordered = filtered
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.OrderCode)
            .ToList();

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return ApiResponse<AdminPaymentListResponse>.Success(
            new AdminPaymentListResponse(items, ordered.Count, page, pageSize, summary));
    }

    public async Task<ApiResponse<AdminPaymentDetailDto>> GetPaymentDetailAsync(string stream, int id, CancellationToken ct = default)
    {
        var normalizedStream = stream?.Trim().ToLowerInvariant();
        if (normalizedStream == "subscription")
        {
            var s = await uow.UserSubscriptions.Query()
                .AsNoTracking()
                .Include(x => x.UserAccount)
                .Include(x => x.SubscriptionPlan)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (s is null) return ApiResponse<AdminPaymentDetailDto>.NotFound("Không tìm thấy giao dịch gói tự học.");

            var name = BuildUserName(s.UserAccount);
            var title = s.PlanNameSnapshot ?? s.SubscriptionPlan?.Name ?? $"Gói #{s.SubscriptionPlanId}";
            var category = s.PlanCategorySnapshot ?? SubscriptionAccessService.ResolveCategory(s.SubscriptionPlan, s.SubscriptionPlanId);
            return ApiResponse<AdminPaymentDetailDto>.Success(new AdminPaymentDetailDto(
                $"sub-{s.Id}",
                "subscription",
                s.Id,
                SubscriptionAccessService.OrderPrefix + (long)s.Id,
                s.UserId,
                name,
                s.UserAccount?.Email ?? string.Empty,
                title,
                category,
                s.AmountPaid,
                "VND",
                s.PaymentStatus.ToString(),
                s.PaymentTransactionId,
                s.PaymentTransactionId,
                NormalizeUtc(s.PurchasedAt == default ? (s.CreatedDate ?? DateTime.UtcNow) : s.PurchasedAt),
                s.PaymentStatus == PaymentStatus.Paid && s.StartDate.HasValue ? NormalizeUtc(s.StartDate.Value) : null,
                s.PaymentExpiresAt.HasValue ? NormalizeUtc(s.PaymentExpiresAt.Value) : null,
                null,
                null,
                s.PaymentStatus == PaymentStatus.Paid ? "Active" : s.PaymentStatus.ToString(),
                s.StartDate.HasValue ? NormalizeUtc(s.StartDate.Value) : null,
                s.EndDate.HasValue ? NormalizeUtc(s.EndDate.Value) : null,
                s.AiGradingQuota,
                s.AiGradingUsedCount,
                s.DurationDaysSnapshot ?? s.SubscriptionPlan?.DurationDays,
                s.PlanRevisionSnapshot,
                null));
        }

        if (normalizedStream == "study_course")
        {
            var c = await uow.StudyCourses.Query()
                .AsNoTracking()
                .Include(x => x.Learner)
                .Include(x => x.Plan)
                .Include(x => x.CertificateLevel)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return ApiResponse<AdminPaymentDetailDto>.NotFound("Không tìm thấy giao dịch khóa học.");

            return ApiResponse<AdminPaymentDetailDto>.Success(new AdminPaymentDetailDto(
                $"course-{c.Id}",
                "study_course",
                c.Id,
                CourseOrderPrefix + c.Id,
                c.LearnerId,
                BuildUserName(c.Learner),
                c.Learner?.Email ?? string.Empty,
                c.Name,
                c.SessionType == "Group" ? "study_course_group" : "study_course",
                c.Amount,
                c.Currency,
                c.PaymentStatus,
                c.PaymentReference,
                c.ProviderPaymentReference,
                NormalizeUtc(c.CreatedAt),
                c.PaymentConfirmedAtUtc.HasValue ? NormalizeUtc(c.PaymentConfirmedAtUtc.Value) : null,
                c.PaymentExpiresAt.HasValue ? NormalizeUtc(c.PaymentExpiresAt.Value) : null,
                SanitizeReason(c.PaymentReviewReason ?? c.CancellationReason),
                c.CohortId,
                c.Status,
                c.StartsAt.HasValue ? NormalizeUtc(c.StartsAt.Value) : null,
                c.EndsAt.HasValue ? NormalizeUtc(c.EndsAt.Value) : null,
                null,
                null,
                null,
                c.PlanRevision,
                c.OrderSnapshot));
        }

        if (normalizedStream == "ai_topup")
        {
            var t = await uow.AiCreditTopUps.Query()
                .AsNoTracking()
                .Include(x => x.UserAccount)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (t is null) return ApiResponse<AdminPaymentDetailDto>.NotFound("Không tìm thấy giao dịch nạp lượt AI.");

            return ApiResponse<AdminPaymentDetailDto>.Success(new AdminPaymentDetailDto(
                $"topup-{t.Id}",
                "ai_topup",
                t.Id,
                TopUpOrderPrefix + t.Id,
                t.UserId,
                BuildUserName(t.UserAccount),
                t.UserAccount?.Email ?? string.Empty,
                $"Nạp {t.Quantity} lượt AI",
                "ai_topup",
                t.TotalAmount,
                "VND",
                t.PaymentStatus.ToString(),
                t.PaymentTransactionId,
                t.PaymentTransactionId,
                NormalizeUtc(t.CreatedDate ?? DateTime.UtcNow),
                t.ActivatedAt.HasValue ? NormalizeUtc(t.ActivatedAt.Value) : null,
                null,
                null,
                null,
                t.PaymentStatus.ToString(),
                t.ActivatedAt.HasValue ? NormalizeUtc(t.ActivatedAt.Value) : null,
                null,
                t.Quantity,
                null,
                null,
                null,
                null));
        }

        return ApiResponse<AdminPaymentDetailDto>.Fail("Luồng giao dịch không hợp lệ.", HttpStatusCode.BadRequest, "INVALID_PAYMENT_STREAM");
    }

    public Task<ApiResponse<PayOsGatewayStatusDto>> GetPayOsStatusAsync(CancellationToken ct = default)
    {
        var payOs = settings.PayOs;
        var clientIdConfigured = !string.IsNullOrWhiteSpace(payOs?.ClientId);
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(payOs?.ApiKey);
        var checksumKeyConfigured = !string.IsNullOrWhiteSpace(payOs?.ChecksumKey);
        var returnValid = IsHttpUrl(payOs?.WebReturnUrl);
        var cancelValid = IsHttpUrl(payOs?.WebCancelUrl);
        var isReady = clientIdConfigured && apiKeyConfigured && checksumKeyConfigured && returnValid && cancelValid;

        var dto = new PayOsGatewayStatusDto(
            clientIdConfigured,
            apiKeyConfigured,
            checksumKeyConfigured,
            isReady,
            true,
            "/api/payment/payos/webhook",
            payOs?.WebReturnUrl,
            payOs?.WebCancelUrl,
            payOs?.MobileLandingBaseUrl,
            ["subscription", "study_course", "ai_topup"]);

        return Task.FromResult(ApiResponse<PayOsGatewayStatusDto>.Success(dto));
    }

    public async Task<ApiResponse<IReadOnlyList<AdminAuditLogDto>>> GetAuditLogsAsync(
        string? entityType,
        int? entityId,
        int take = 50,
        CancellationToken ct = default)
    {
        var clampedTake = Math.Clamp(take, 1, 100);
        var q = uow.AdminAuditLogs.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var types = entityType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(10)
                .ToList();
            q = types.Count == 1 ? q.Where(x => x.EntityType == types[0]) : q.Where(x => types.Contains(x.EntityType));
        }
        if (entityId.HasValue)
            q = q.Where(x => x.EntityId == entityId.Value);

        var rows = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(clampedTake)
            .ToListAsync(ct);

        var actorIds = rows.Where(r => r.ActorUserId.HasValue).Select(r => r.ActorUserId!.Value).Distinct().ToList();
        var actors = actorIds.Count == 0
            ? new Dictionary<int, string>()
            : (await uow.UserAccounts.Query()
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync(ct))
                .ToDictionary(u => u.Id, u =>
                {
                    var full = $"{u.FirstName} {u.LastName}".Trim();
                    return string.IsNullOrEmpty(full) ? u.Email : full;
                });

        var result = rows.Select(r => new AdminAuditLogDto(
            r.Id,
            r.ActorUserId,
            r.ActorUserId.HasValue && actors.TryGetValue(r.ActorUserId.Value, out var name) ? name : "Quản trị viên",
            r.EntityType,
            r.EntityId,
            r.Action,
            r.BeforeJson,
            r.AfterJson,
            r.Reason,
            NormalizeUtc(r.CreatedAtUtc))).ToList();

        return ApiResponse<IReadOnlyList<AdminAuditLogDto>>.Success(result);
    }

    private async Task<List<AdminPaymentTransactionDto>> LoadAllTransactionsAsync(CancellationToken ct)
    {
        var subs = await uow.UserSubscriptions.Query()
            .AsNoTracking()
            .Include(x => x.UserAccount)
            .Include(x => x.SubscriptionPlan)
            .Where(x => !x.IsDeleted)
            .ToListAsync(ct);

        var courses = await uow.StudyCourses.Query()
            .AsNoTracking()
            .Include(x => x.Learner)
            .ToListAsync(ct);

        var topUps = await uow.AiCreditTopUps.Query()
            .AsNoTracking()
            .Include(x => x.UserAccount)
            .Where(x => !x.IsDeleted)
            .ToListAsync(ct);

        var list = new List<AdminPaymentTransactionDto>(subs.Count + courses.Count + topUps.Count);

        foreach (var s in subs)
        {
            var created = s.PurchasedAt == default ? (s.CreatedDate ?? DateTime.UtcNow) : s.PurchasedAt;
            list.Add(new AdminPaymentTransactionDto(
                $"sub-{s.Id}",
                "subscription",
                s.Id,
                SubscriptionAccessService.OrderPrefix + (long)s.Id,
                s.UserId,
                BuildUserName(s.UserAccount),
                s.UserAccount?.Email ?? string.Empty,
                s.PlanNameSnapshot ?? s.SubscriptionPlan?.Name ?? $"Gói #{s.SubscriptionPlanId}",
                s.PlanCategorySnapshot ?? SubscriptionAccessService.ResolveCategory(s.SubscriptionPlan, s.SubscriptionPlanId),
                s.AmountPaid,
                "VND",
                s.PaymentStatus.ToString(),
                s.PaymentTransactionId,
                NormalizeUtc(created),
                s.PaymentStatus == PaymentStatus.Paid && s.StartDate.HasValue ? NormalizeUtc(s.StartDate.Value) : null,
                s.PaymentExpiresAt.HasValue ? NormalizeUtc(s.PaymentExpiresAt.Value) : null,
                null));
        }

        foreach (var c in courses)
        {
            list.Add(new AdminPaymentTransactionDto(
                $"course-{c.Id}",
                "study_course",
                c.Id,
                CourseOrderPrefix + c.Id,
                c.LearnerId,
                BuildUserName(c.Learner),
                c.Learner?.Email ?? string.Empty,
                c.Name,
                c.SessionType == "Group" ? "study_course_group" : "study_course",
                c.Amount,
                c.Currency,
                c.PaymentStatus,
                c.ProviderPaymentReference ?? c.PaymentReference,
                NormalizeUtc(c.CreatedAt),
                c.PaymentConfirmedAtUtc.HasValue ? NormalizeUtc(c.PaymentConfirmedAtUtc.Value) : null,
                c.PaymentExpiresAt.HasValue ? NormalizeUtc(c.PaymentExpiresAt.Value) : null,
                SanitizeReason(c.PaymentReviewReason ?? c.CancellationReason)));
        }

        foreach (var t in topUps)
        {
            list.Add(new AdminPaymentTransactionDto(
                $"topup-{t.Id}",
                "ai_topup",
                t.Id,
                TopUpOrderPrefix + t.Id,
                t.UserId,
                BuildUserName(t.UserAccount),
                t.UserAccount?.Email ?? string.Empty,
                $"Nạp {t.Quantity} lượt AI",
                "ai_topup",
                t.TotalAmount,
                "VND",
                t.PaymentStatus.ToString(),
                t.PaymentTransactionId,
                NormalizeUtc(t.CreatedDate ?? DateTime.UtcNow),
                t.ActivatedAt.HasValue ? NormalizeUtc(t.ActivatedAt.Value) : null,
                null,
                null));
        }

        return list;
    }

    private static AdminFeatureItemDto ToFeatureDto(Feature f)
        => new(
            f.Id,
            f.Code,
            f.Name,
            f.Description,
            f.Category,
            SubscriptionAccessService.EffectiveMode(f),
            f.IsCoreFree,
            f.CreditFeatureKey,
            SubscriptionAccessService.ParseFeatureRoutes(f.RoutePrefixesJson),
            f.SortOrder,
            f.SubscriptionPlans.Count(p => !p.IsDeleted && p.MeetingSessionCount == 0 &&
                SubscriptionAccessService.ResolveCategory(p, p.Id) != SubscriptionAccessService.CategoryMentorLegacy),
            FeatureAccessModes.IsSuspended(f.AccessMode) ? FeatureAccessModes.AvailabilityDisabled : FeatureAccessModes.AvailabilityActive,
            FeatureAccessModes.PolicyOf(f.AccessMode),
            StoredStateOf(f));

    /// <summary>The raw stored availability+policy value, echoed back by the Admin UI for optimistic concurrency.</summary>
    private static string StoredStateOf(Feature f)
        => string.IsNullOrWhiteSpace(f.AccessMode) ? FeatureAccessModes.Free : f.AccessMode.Trim().ToUpperInvariant();

    /// <summary>
    /// "SaleStopped" / "SaleResumed" when only the selling status changed (so history never reads "deleted" for a plan
    /// that was merely taken off sale), otherwise "Updated". Revision is ignored because every save bumps it.
    /// </summary>
    private static string PlanAuditAction(string? beforeJson, string afterJson)
    {
        if (string.IsNullOrWhiteSpace(beforeJson)) return "Updated";
        try
        {
            using var before = JsonDocument.Parse(beforeJson);
            using var after = JsonDocument.Parse(afterJson);
            var changed = new List<string>();
            foreach (var property in after.RootElement.EnumerateObject())
            {
                if (property.Name == nameof(SubscriptionPlan.Revision)) continue;
                if (!before.RootElement.TryGetProperty(property.Name, out var old) || old.GetRawText() != property.Value.GetRawText())
                    changed.Add(property.Name);
            }
            if (changed.Count == 1 && changed[0] == nameof(SubscriptionPlan.IsActive))
                return after.RootElement.GetProperty(nameof(SubscriptionPlan.IsActive)).GetBoolean() ? "SaleResumed" : "SaleStopped";
            return "Updated";
        }
        catch (JsonException)
        {
            return "Updated";
        }
    }

    private static AdminSubscriptionPlanDto ToPlanDto(SubscriptionPlan p, int totalPurchases, int paidPurchases)
        => new(
            p.Id,
            p.Name,
            p.Description,
            SubscriptionAccessService.ResolveCategory(p, p.Id),
            p.DurationDays,
            p.Price,
            p.AiGradingQuota,
            p.MeetingSessionCount,
            p.IsPopular,
            p.IsTrial,
            p.IsActive,
            p.SortOrder,
            p.Revision,
            totalPurchases,
            paidPurchases,
            p.Features.Select(f => f.Id).OrderBy(x => x).ToList(),
            p.CreatedDate.HasValue ? NormalizeUtc(p.CreatedDate.Value) : null,
            p.ModifiedDate.HasValue ? NormalizeUtc(p.ModifiedDate.Value) : null);

    private static string BuildUserName(UserAccount? user)
    {
        if (user is null) return "Học viên";
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(full) ? user.Email : full;
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static DateTime? ParseBoundaryDate(string? raw, bool isEnd)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (DateOnly.TryParse(text, out var dateOnly))
        {
            var localStart = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), VietnamOffset);
            return isEnd
                ? localStart.AddDays(1).AddTicks(-1).UtcDateTime
                : localStart.UtcDateTime;
        }
        if (DateTimeOffset.TryParse(text, out var dto))
            return dto.UtcDateTime;
        return null;
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
