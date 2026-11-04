using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

// Dịch vụ quản lý sổ cái tín dụng lượt học Mentor với khả năng kiểm toán, chống trừ lặp và bất biến.
public sealed class MentorCreditLedgerService : IMentorCreditLedgerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimService _claimService;
    private readonly ILogger<MentorCreditLedgerService> _logger;

    public MentorCreditLedgerService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        ILogger<MentorCreditLedgerService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimService = claimService;
        _logger = logger;
    }

    public async Task<ApiResponse<LearnerCreditBalanceResponse>> GetMyCreditBalanceAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Learner)
                return ApiResponse<LearnerCreditBalanceResponse>.Fail("Chỉ tài khoản Học viên mới có thể xem số dư lượt tư vấn.", HttpStatusCode.Forbidden);

            var nowUtc = DateTime.UtcNow;
            var activeGrants = await _unitOfWork.MentorCreditLedger.GetActiveGrantsForLearnerAsync(claim.Id, nowUtc, cancellationToken);
            var recentEntries = await _unitOfWork.MentorCreditLedger.GetEntriesByLearnerIdAsync(claim.Id, 0, 20, cancellationToken);

            var totalAvailable = activeGrants.Sum(x => x.AvailableQuantity);
            var earliestExpiry = activeGrants.OrderBy(x => x.ExpiresAtUtc).FirstOrDefault()?.ExpiresAtUtc;

            var response = new LearnerCreditBalanceResponse
            {
                TotalAvailableCredits = totalAvailable,
                ExpiringSoonestAtUtc = earliestExpiry,
                ActiveGrants = activeGrants.Select(MapGrantItem).ToList(),
                RecentEntries = recentEntries.Select(e => new CreditEntryItemResponse
                {
                    EntryId = e.Id,
                    CreditGrantId = e.CreditGrantId,
                    EntryType = e.EntryType.ToString(),
                    Amount = e.Amount,
                    OperationKey = e.OperationKey,
                    MentorBookingId = e.MentorBookingId,
                    Reason = e.Reason,
                    CreatedAtUtc = e.CreatedAtUtc
                }).ToList()
            };

            return ApiResponse<LearnerCreditBalanceResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy số dư tín dụng của học viên.");
            return ApiResponse<LearnerCreditBalanceResponse>.Fail($"Không thể tải số dư: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<CreditEntryItemResponse>>> GetMyCreditHistoryAsync(
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Learner)
                return ApiResponse<List<CreditEntryItemResponse>>.Fail("Chỉ tài khoản Học viên mới có thể xem lịch sử biến động.", HttpStatusCode.Forbidden);

            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 20;
            var skip = (page - 1) * pageSize;

            var entries = await _unitOfWork.MentorCreditLedger.GetEntriesByLearnerIdAsync(claim.Id, skip, pageSize, cancellationToken);
            var result = entries.Select(e => new CreditEntryItemResponse
            {
                EntryId = e.Id,
                CreditGrantId = e.CreditGrantId,
                EntryType = e.EntryType.ToString(),
                Amount = e.Amount,
                OperationKey = e.OperationKey,
                MentorBookingId = e.MentorBookingId,
                Reason = e.Reason,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToList();

            return ApiResponse<List<CreditEntryItemResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy lịch sử biến động tín dụng của học viên.");
            return ApiResponse<List<CreditEntryItemResponse>>.Fail($"Không thể tải lịch sử: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<CreditGrantItemResponse>> CreateGrantFromPaidSubscriptionAsync(
        int subscriptionId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _unitOfWork.UserSubscriptions.GetByIdAsync(subscriptionId, cancellationToken);

            if (subscription is null)
                return ApiResponse<CreditGrantItemResponse>.Fail("Không tìm thấy đơn hàng gói học.", HttpStatusCode.NotFound);

            if (subscription.PaymentStatus != PaymentStatus.Paid)
                return ApiResponse<CreditGrantItemResponse>.Fail("Chỉ gói học đã thanh toán thành công (Paid) mới được cấp lượt tư vấn.", HttpStatusCode.BadRequest);

            if (subscription.MeetingSessionsIncluded <= 0)
                return ApiResponse<CreditGrantItemResponse>.Fail("Gói này không có vé tư vấn 1-1.", HttpStatusCode.BadRequest);

            var existingGrant = await _unitOfWork.MentorCreditLedger.GetGrantBySubscriptionIdAsync(subscriptionId, cancellationToken);
            if (existingGrant is not null)
            {
                return ApiResponse<CreditGrantItemResponse>.Success(
                    MapGrantItem(existingGrant),
                    message: "Nguồn cấp tín dụng cho đơn hàng này đã tồn tại (idempotent).");
            }

            var opKey = !string.IsNullOrWhiteSpace(idempotencyKey)
                ? idempotencyKey
                : $"grant:sub:{subscriptionId}";

            var existingEntry = await _unitOfWork.MentorCreditLedger.GetEntryByOperationKeyAsync(opKey, cancellationToken);
            if (existingEntry is not null)
            {
                var linkedGrant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(existingEntry.CreditGrantId, cancellationToken);
                if (linkedGrant is not null)
                {
                    return ApiResponse<CreditGrantItemResponse>.Success(
                        MapGrantItem(linkedGrant),
                        message: "Thao tác cấp tín dụng đã được ghi nhận trước đó.");
                }
            }

            var usedSessions = Math.Clamp(subscription.MeetingSessionsUsed, 0, subscription.MeetingSessionsIncluded);
            var remainingSessions = subscription.MeetingSessionsIncluded - usedSessions;
            if (remainingSessions == 0)
                return ApiResponse<CreditGrantItemResponse>.Fail("Gói tư vấn đã sử dụng hết vé.", HttpStatusCode.BadRequest);

            var effectiveFrom = subscription.StartDate ?? subscription.PurchasedAt;
            var validityDays = subscription.MeetingSessionsIncluded >= 3 ? 90 : 30;
            var expiresAt = subscription.EndDate ?? effectiveFrom.AddDays(validityDays);

            var certificateLevelId = subscription.CertificateLevelId;
            if (!certificateLevelId.HasValue && subscription.SubscriptionPlanId > 0)
            {
                var plan = await _unitOfWork.SubscriptionPlans.GetByIdAsync(subscription.SubscriptionPlanId, cancellationToken);
                certificateLevelId = plan?.CertificateLevelId;
            }

            var grant = new MentorCreditGrant
            {
                LearnerId = subscription.UserId,
                UserSubscriptionId = subscription.Id,
                CertificateLevelId = certificateLevelId,
                PolicyVersion = 3,
                InitialQuantity = remainingSessions,
                AvailableQuantity = remainingSessions,
                EffectiveFromUtc = DateTime.SpecifyKind(effectiveFrom, DateTimeKind.Utc),
                ExpiresAtUtc = DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.MentorCreditLedger.AddGrantAsync(grant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var entry = new MentorCreditEntry
            {
                CreditGrantId = grant.Id,
                LearnerId = subscription.UserId,
                EntryType = CreditEntryType.Grant,
                Amount = remainingSessions,
                OperationKey = opKey,
                MentorBookingId = null,
                Reason = $"Cấp {remainingSessions} lượt tư vấn còn lại từ đơn hàng đã thanh toán #{subscriptionId}",
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.MentorCreditLedger.AddEntryAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<CreditGrantItemResponse>.Success(
                MapGrantItem(grant),
                message: "Cấp lượt tư vấn vào sổ cái tín dụng thành công.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Xung đột ghi nhận grant (OperationKey trùng lặp): sub {SubId}", subscriptionId);
            var reloaded = await _unitOfWork.MentorCreditLedger.GetGrantBySubscriptionIdAsync(subscriptionId, cancellationToken);
            if (reloaded is not null)
            {
                return ApiResponse<CreditGrantItemResponse>.Success(
                    MapGrantItem(reloaded),
                    message: "Nguồn cấp tín dụng đã được ghi nhận trước đó.");
            }
            return ApiResponse<CreditGrantItemResponse>.Fail("Xảy ra xung đột khi ghi nhận nguồn cấp tín dụng.", HttpStatusCode.Conflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cấp tín dụng từ gói học {SubId}.", subscriptionId);
            return ApiResponse<CreditGrantItemResponse>.Fail($"Không thể cấp lượt tư vấn: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<CreditSpendResult> SpendCreditAsync(
        int learnerId, int bookingId, string operationKey, DateTime appointmentTimeUtc, CancellationToken cancellationToken = default)
    {
        var existingEntry = await _unitOfWork.MentorCreditLedger.GetEntryByOperationKeyAsync(operationKey, cancellationToken);
        if (existingEntry is not null)
        {
            var existingGrant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(existingEntry.CreditGrantId, cancellationToken);
            return new CreditSpendResult
            {
                IsSuccess = true,
                CreditGrantId = existingEntry.CreditGrantId,
                EntryId = existingEntry.Id,
                RemainingBalance = existingGrant?.AvailableQuantity ?? 0,
                OperationKey = operationKey
            };
        }

        var bookingEntries = await _unitOfWork.MentorCreditLedger.GetEntriesByBookingIdAsync(bookingId, cancellationToken);
        var existingSpend = bookingEntries.FirstOrDefault(e => e.EntryType == CreditEntryType.Spend);
        if (existingSpend is not null)
        {
            var linkedGrant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(existingSpend.CreditGrantId, cancellationToken);
            return new CreditSpendResult
            {
                IsSuccess = true,
                CreditGrantId = existingSpend.CreditGrantId,
                EntryId = existingSpend.Id,
                RemainingBalance = linkedGrant?.AvailableQuantity ?? 0,
                OperationKey = existingSpend.OperationKey
            };
        }

        var nowUtc = DateTime.UtcNow;
        var activeGrants = await _unitOfWork.MentorCreditLedger.GetActiveGrantsForLearnerAsync(learnerId, nowUtc, cancellationToken);

        var eligibleGrant = activeGrants.FirstOrDefault(g =>
            g.AvailableQuantity > 0 &&
            g.EffectiveFromUtc <= appointmentTimeUtc &&
            appointmentTimeUtc <= g.ExpiresAtUtc);

        if (eligibleGrant is null)
        {
            return new CreditSpendResult
            {
                IsSuccess = false,
                ErrorMessage = "Bạn không còn đủ lượt tư vấn hợp lệ hoặc lượt tư vấn đã hết hạn trước thời điểm diễn ra buổi hẹn.",
                OperationKey = operationKey
            };
        }

        eligibleGrant.AvailableQuantity--;
        _unitOfWork.MentorCreditLedger.UpdateGrant(eligibleGrant);

        var spendEntry = new MentorCreditEntry
        {
            CreditGrantId = eligibleGrant.Id,
            LearnerId = learnerId,
            EntryType = CreditEntryType.Spend,
            Amount = -1,
            OperationKey = operationKey,
            MentorBookingId = bookingId,
            Reason = $"Trừ 1 lượt tư vấn cho buổi hẹn #{bookingId}",
            CreatedAtUtc = DateTime.UtcNow
        };
        await _unitOfWork.MentorCreditLedger.AddEntryAsync(spendEntry, cancellationToken);

        var subscription = await _unitOfWork.UserSubscriptions.Query()
            .FirstOrDefaultAsync(x => x.Id == eligibleGrant.UserSubscriptionId, cancellationToken);
        if (subscription is not null)
        {
            subscription.MeetingSessionsUsed = Math.Min(subscription.MeetingSessionsIncluded, subscription.MeetingSessionsUsed + 1);
            _unitOfWork.UserSubscriptions.Update(subscription);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreditSpendResult
        {
            IsSuccess = true,
            CreditGrantId = eligibleGrant.Id,
            EntryId = spendEntry.Id,
            RemainingBalance = eligibleGrant.AvailableQuantity,
            OperationKey = operationKey
        };
    }

    public async Task<CreditRefundResult> RefundCreditAsync(
        int learnerId, int bookingId, string operationKey, string reason, CancellationToken cancellationToken = default)
    {
        var existingEntry = await _unitOfWork.MentorCreditLedger.GetEntryByOperationKeyAsync(operationKey, cancellationToken);
        if (existingEntry is not null)
        {
            var linkedGrant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(existingEntry.CreditGrantId, cancellationToken);
            return new CreditRefundResult
            {
                IsSuccess = true,
                CreditGrantId = existingEntry.CreditGrantId,
                EntryId = existingEntry.Id,
                RestoredBalance = linkedGrant?.AvailableQuantity ?? 0,
                OperationKey = operationKey
            };
        }

        var bookingEntries = await _unitOfWork.MentorCreditLedger.GetEntriesByBookingIdAsync(bookingId, cancellationToken);
        var existingRefund = bookingEntries.FirstOrDefault(e => e.EntryType == CreditEntryType.Refund);
        if (existingRefund is not null)
        {
            var linkedGrant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(existingRefund.CreditGrantId, cancellationToken);
            return new CreditRefundResult
            {
                IsSuccess = true,
                CreditGrantId = existingRefund.CreditGrantId,
                EntryId = existingRefund.Id,
                RestoredBalance = linkedGrant?.AvailableQuantity ?? 0,
                OperationKey = existingRefund.OperationKey
            };
        }

        var originalSpend = bookingEntries.FirstOrDefault(e => e.EntryType == CreditEntryType.Spend);
        if (originalSpend is null)
        {
            return new CreditRefundResult
            {
                IsSuccess = false,
                ErrorMessage = "Không tìm thấy bút toán trừ lượt tư vấn ban đầu của buổi hẹn này.",
                OperationKey = operationKey
            };
        }

        var grant = await _unitOfWork.MentorCreditLedger.GetGrantByIdAsync(originalSpend.CreditGrantId, cancellationToken);
        if (grant is null)
        {
            return new CreditRefundResult
            {
                IsSuccess = false,
                ErrorMessage = "Không tìm thấy nguồn cấp tín dụng ban đầu để hoàn lượt.",
                OperationKey = operationKey
            };
        }

        grant.AvailableQuantity = Math.Min(grant.InitialQuantity, grant.AvailableQuantity + 1);
        _unitOfWork.MentorCreditLedger.UpdateGrant(grant);

        var refundEntry = new MentorCreditEntry
        {
            CreditGrantId = grant.Id,
            LearnerId = learnerId,
            EntryType = CreditEntryType.Refund,
            Amount = 1,
            OperationKey = operationKey,
            MentorBookingId = bookingId,
            Reason = string.IsNullOrWhiteSpace(reason) ? $"Hoàn lại 1 lượt tư vấn cho buổi hẹn #{bookingId}" : reason,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _unitOfWork.MentorCreditLedger.AddEntryAsync(refundEntry, cancellationToken);

        var subscription = await _unitOfWork.UserSubscriptions.Query()
            .FirstOrDefaultAsync(x => x.Id == grant.UserSubscriptionId, cancellationToken);
        if (subscription is not null)
        {
            subscription.MeetingSessionsUsed = Math.Max(0, subscription.MeetingSessionsUsed - 1);
            _unitOfWork.UserSubscriptions.Update(subscription);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreditRefundResult
        {
            IsSuccess = true,
            CreditGrantId = grant.Id,
            EntryId = refundEntry.Id,
            RestoredBalance = grant.AvailableQuantity,
            OperationKey = operationKey
        };
    }

    public async Task<ApiResponse<CreditReconciliationResponse>> ReconcileLearnerCreditsForAdminAsync(
        int learnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<CreditReconciliationResponse>.Fail("Chỉ Quản trị viên mới có quyền thực hiện đối soát sổ cái tín dụng.", HttpStatusCode.Forbidden);

            var grants = await _unitOfWork.MentorCreditLedger.GetAllGrantsForLearnerAsync(learnerId, cancellationToken);
            var entries = await _unitOfWork.MentorCreditLedger.GetEntriesByLearnerIdAsync(learnerId, 0, 10000, cancellationToken);

            var totalIssued = grants.Sum(x => x.InitialQuantity);
            var totalAvailable = grants.Sum(x => x.AvailableQuantity);
            var totalEntriesNet = entries.Sum(x => x.Amount);

            var isConsistent = totalAvailable == totalEntriesNet;
            var discrepancy = totalAvailable - totalEntriesNet;

            var response = new CreditReconciliationResponse
            {
                LearnerId = learnerId,
                TotalGrantsIssued = totalIssued,
                TotalGrantsAvailable = totalAvailable,
                TotalEntriesNet = totalEntriesNet,
                IsConsistent = isConsistent,
                Discrepancy = discrepancy
            };

            return ApiResponse<CreditReconciliationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi Quản trị viên đối soát sổ cái học viên {LearnerId}.", learnerId);
            return ApiResponse<CreditReconciliationResponse>.Fail($"Không thể đối soát: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private static CreditGrantItemResponse MapGrantItem(MentorCreditGrant g) => new()
    {
        GrantId = g.Id,
        UserSubscriptionId = g.UserSubscriptionId,
        CertificateLevelId = g.CertificateLevelId,
        JlptLevel = g.CertificateLevel?.Code ?? MentorPlanService.ResolveLevelCode(g.CertificateLevelId),
        PolicyVersion = g.PolicyVersion,
        InitialQuantity = g.InitialQuantity,
        AvailableQuantity = g.AvailableQuantity,
        EffectiveFromUtc = g.EffectiveFromUtc,
        ExpiresAtUtc = g.ExpiresAtUtc,
        CreatedAtUtc = g.CreatedAtUtc
    };
}
