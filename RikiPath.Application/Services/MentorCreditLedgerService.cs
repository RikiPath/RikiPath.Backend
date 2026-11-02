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

    // Học viên xem số dư lượt tư vấn khả dụng, thời hạn gói và lịch sử biến động.
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
                ActiveGrants = activeGrants.Select(g => new CreditGrantItemResponse
                {
                    GrantId = g.Id,
                    UserSubscriptionId = g.UserSubscriptionId,
                    PolicyVersion = g.PolicyVersion,
                    InitialQuantity = g.InitialQuantity,
                    AvailableQuantity = g.AvailableQuantity,
                    EffectiveFromUtc = g.EffectiveFromUtc,
                    ExpiresAtUtc = g.ExpiresAtUtc,
                    CreatedAtUtc = g.CreatedAtUtc
                }).ToList(),
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

    // Học viên xem chi tiết lịch sử bút toán sổ cái có phân trang.
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

    // Cấp lượt tư vấn mới từ gói học đã thanh toán thành công (Paid) theo nguyên tắc bất biến và idempotent.
    public async Task<ApiResponse<CreditGrantItemResponse>> CreateGrantFromPaidSubscriptionAsync(
        int subscriptionId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Kiểm tra đơn hàng UserSubscription
            var subscription = await _unitOfWork.UserSubscriptions.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);

            if (subscription is null)
                return ApiResponse<CreditGrantItemResponse>.Fail("Không tìm thấy đơn hàng gói học.", HttpStatusCode.NotFound);

            if (subscription.PaymentStatus != PaymentStatus.Paid)
                return ApiResponse<CreditGrantItemResponse>.Fail("Chỉ gói học đã thanh toán thành công (Paid) mới được cấp lượt tư vấn.", HttpStatusCode.BadRequest);

            if (subscription.MeetingSessionsIncluded <= 0)
                return ApiResponse<CreditGrantItemResponse>.Fail("Gói này không có vé tư vấn 1-1.", HttpStatusCode.BadRequest);

            // 2. Chống cấp trùng (Idempotent replay): Kiểm tra xem subscription này đã được cấp grant chưa
            var existingGrant = await _unitOfWork.MentorCreditLedger.GetGrantBySubscriptionIdAsync(subscriptionId, cancellationToken);
            if (existingGrant is not null)
            {
                return ApiResponse<CreditGrantItemResponse>.Success(new CreditGrantItemResponse
                {
                    GrantId = existingGrant.Id,
                    UserSubscriptionId = existingGrant.UserSubscriptionId,
                    PolicyVersion = existingGrant.PolicyVersion,
                    InitialQuantity = existingGrant.InitialQuantity,
                    AvailableQuantity = existingGrant.AvailableQuantity,
                    EffectiveFromUtc = existingGrant.EffectiveFromUtc,
                    ExpiresAtUtc = existingGrant.ExpiresAtUtc,
                    CreatedAtUtc = existingGrant.CreatedAtUtc
                }, message: "Nguồn cấp tín dụng cho đơn hàng này đã tồn tại (idempotent).");
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
                    return ApiResponse<CreditGrantItemResponse>.Success(new CreditGrantItemResponse
                    {
                        GrantId = linkedGrant.Id,
                        UserSubscriptionId = linkedGrant.UserSubscriptionId,
                        PolicyVersion = linkedGrant.PolicyVersion,
                        InitialQuantity = linkedGrant.InitialQuantity,
                        AvailableQuantity = linkedGrant.AvailableQuantity,
                        EffectiveFromUtc = linkedGrant.EffectiveFromUtc,
                        ExpiresAtUtc = linkedGrant.ExpiresAtUtc,
                        CreatedAtUtc = linkedGrant.CreatedAtUtc
                    }, message: "Thao tác cấp tín dụng đã được ghi nhận trước đó.");
                }
            }

            // Gói cũ có thể đã sử dụng lượt trước khi sổ cái được tạo. Chỉ chuyển phần quyền lợi còn lại.
            var usedSessions = Math.Clamp(subscription.MeetingSessionsUsed, 0, subscription.MeetingSessionsIncluded);
            var remainingSessions = subscription.MeetingSessionsIncluded - usedSessions;
            if (remainingSessions == 0)
                return ApiResponse<CreditGrantItemResponse>.Fail("Gói tư vấn đã sử dụng hết vé.", HttpStatusCode.BadRequest);

            // 3. Tính toán thời hạn hiệu lực theo quy tắc Product Baseline V3:
            // Gói 1 buổi: có giá trị trong 30 ngày.
            // Gói 3 buổi: có giá trị trong 90 ngày.
            var effectiveFrom = subscription.StartDate ?? subscription.PurchasedAt;
            var validityDays = subscription.MeetingSessionsIncluded >= 3 ? 90 : 30;
            var expiresAt = subscription.EndDate ?? effectiveFrom.AddDays(validityDays);

            var grant = new MentorCreditGrant
            {
                LearnerId = subscription.UserId,
                UserSubscriptionId = subscription.Id,
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

            return ApiResponse<CreditGrantItemResponse>.Success(new CreditGrantItemResponse
            {
                GrantId = grant.Id,
                UserSubscriptionId = grant.UserSubscriptionId,
                PolicyVersion = grant.PolicyVersion,
                InitialQuantity = grant.InitialQuantity,
                AvailableQuantity = grant.AvailableQuantity,
                EffectiveFromUtc = grant.EffectiveFromUtc,
                ExpiresAtUtc = grant.ExpiresAtUtc,
                CreatedAtUtc = grant.CreatedAtUtc
            }, message: "Cấp lượt tư vấn vào sổ cái tín dụng thành công.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Xung đột ghi nhận grant (OperationKey trùng lặp): sub {SubId}", subscriptionId);
            var reloaded = await _unitOfWork.MentorCreditLedger.GetGrantBySubscriptionIdAsync(subscriptionId, cancellationToken);
            if (reloaded is not null)
            {
                return ApiResponse<CreditGrantItemResponse>.Success(new CreditGrantItemResponse
                {
                    GrantId = reloaded.Id,
                    UserSubscriptionId = reloaded.UserSubscriptionId,
                    PolicyVersion = reloaded.PolicyVersion,
                    InitialQuantity = reloaded.InitialQuantity,
                    AvailableQuantity = reloaded.AvailableQuantity,
                    EffectiveFromUtc = reloaded.EffectiveFromUtc,
                    ExpiresAtUtc = reloaded.ExpiresAtUtc,
                    CreatedAtUtc = reloaded.CreatedAtUtc
                }, message: "Nguồn cấp tín dụng đã được ghi nhận trước đó.");
            }
            return ApiResponse<CreditGrantItemResponse>.Fail("Xảy ra xung đột khi ghi nhận nguồn cấp tín dụng.", HttpStatusCode.Conflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cấp tín dụng từ gói học {SubId}.", subscriptionId);
            return ApiResponse<CreditGrantItemResponse>.Fail($"Không thể cấp lượt tư vấn: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    // Trừ 1 lượt tư vấn theo nguyên tắc FIFO khi đặt lịch thành công (tham gia vào atomic booking transaction).
    public async Task<CreditSpendResult> SpendCreditAsync(
        int learnerId, int bookingId, string operationKey, DateTime appointmentTimeUtc, CancellationToken cancellationToken = default)
    {
        // 1. Chống trừ trùng (Idempotent replay): Nếu OperationKey đã ghi nhận thì trả về thành công ngay
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

        // Kiểm tra xem bookingId này đã bị trừ lượt trước đó chưa
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

        // 2. Tìm nguồn cấp khả dụng theo FIFO (hạn sớm dùng trước)
        var nowUtc = DateTime.UtcNow;
        var activeGrants = await _unitOfWork.MentorCreditLedger.GetActiveGrantsForLearnerAsync(learnerId, nowUtc, cancellationToken);

        // Buổi hẹn phải nằm trong thời hạn hiệu lực của nguồn cấp
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

        // 3. Trừ 1 lượt khỏi số dư của nguồn cấp
        eligibleGrant.AvailableQuantity--;
        _unitOfWork.MentorCreditLedger.UpdateGrant(eligibleGrant);

        // 4. Ghi bút toán append-only vào sổ cái
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

        // 5. Đồng bộ counter gói học lịch sử UserSubscription
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

    // Hoàn lại 1 lượt tư vấn về đúng nguồn cấp ban đầu khi buổi hẹn bị hủy hợp lệ.
    public async Task<CreditRefundResult> RefundCreditAsync(
        int learnerId, int bookingId, string operationKey, string reason, CancellationToken cancellationToken = default)
    {
        // 1. Chống hoàn trùng (Idempotent replay)
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

        // Kiểm tra xem bookingId đã được hoàn trước đó chưa
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

        // Tìm bút toán trừ Spend ban đầu của booking này để hoàn về đúng nguồn cấp
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

        // 2. Hoàn lại 1 lượt vào nguồn cấp ban đầu (không được vượt quá InitialQuantity)
        grant.AvailableQuantity = Math.Min(grant.InitialQuantity, grant.AvailableQuantity + 1);
        _unitOfWork.MentorCreditLedger.UpdateGrant(grant);

        // 3. Ghi bút toán Refund vào sổ cái
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

        // 4. Đồng bộ counter gói học lịch sử UserSubscription
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

    // Quản trị viên đối soát tính toàn vẹn và khớp đúng giữa sổ cái bút toán và số dư nguồn cấp của học viên.
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

            // Bút toán Spend có Amount = -1, Grant có Amount = +N, Refund có Amount = +1
            var totalEntriesNet = entries.Sum(x => x.Amount);

            // Số dư còn lại phải đúng bằng tổng InitialQuantity + tổng các bút toán trừ/hoàn
            // Hoặc: tổng available phải bằng totalEntriesNet
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
}
