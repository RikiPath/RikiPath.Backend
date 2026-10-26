using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>Owns Mentor Meeting validation, booking lifecycle, checkout orchestration, and response mapping.</summary>
public sealed class MentorMeetingService(
    IUnitOfWork unitOfWork,
    IPaymentGatewayClient paymentGateway,
    IClaimService claimService,
    AppSettings appSettings,
    ILogger<MentorMeetingService> logger) : IMentorMeetingService
{
    /// <summary>Mentor phải đăng ký lịch trước thời điểm bắt đầu ít nhất số tháng này (admin dựa vào tiêu chí này để duyệt).</summary>
    private const int MinAdvanceMonths = 1;

    /// <summary>Mentor chỉ được yêu cầu hủy lịch trống (chưa có người đặt) trước giờ bắt đầu ít nhất số ngày này.</summary>
    private const int MinCancelAdvanceDays = 7;

    /// <summary>Mỗi slot mà learner đặt dài bao nhiêu phút. Server tự chia khoảng mentor đăng ký thành các slot này.</summary>
    private const int SlotMinutes = 60;

    /// <summary>Tối đa số slot xử lý trong một lần gọi duyệt/hủy hàng loạt.</summary>
    private const int MaxBulkSlots = 50;

    /// <summary>Một lần đăng ký (cả khoảng) không được dài quá mức này.</summary>
    private static readonly TimeSpan MaxSlotDuration = TimeSpan.FromHours(4);

    public async Task<ApiResponse<List<BookableMentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken)
    {
        var mentors = await unitOfWork.UserAccounts.FindAsync(
            x => x.Role == Role.Mentor && !x.IsDeleted, cancellationToken);
        return ApiResponse<List<BookableMentorResponse>>.Success(mentors
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id)
            .Select(x => new BookableMentorResponse
            {
                Id = x.Id,
                Name = $"{x.FirstName} {x.LastName}".Trim(),
                AvatarUrl = x.AvatarUrl
            }).ToList());
    }

    public async Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken)
    {
        try
        {
            var plans = await unitOfWork.SubscriptionPlans.GetMeetingPlansAsync(cancellationToken);
            return ApiResponse<List<MentorMeetingPlanResponse>>.Success(plans.Select(MapPlan).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load Mentor Meeting plans.");
            return ApiResponse<List<MentorMeetingPlanResponse>>.Fail("Không thể tải danh sách gói meeting.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetAvailableSlotsAsync(
        DateTime from, DateTime to, int? mentorId, CancellationToken cancellationToken)
    {
        if (to <= from || to - from > TimeSpan.FromDays(90))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Khoảng thời gian không hợp lệ (tối đa 90 ngày).");
        if (mentorId is <= 0)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("mentorId phải lớn hơn 0.");

        try
        {
            await ExpirePendingPaymentsAsync(cancellationToken);
            var slots = await unitOfWork.MentorAvailabilities.GetAvailableSlotsAsync(mentorId, from, to, cancellationToken);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.Select(MapAvailability).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load available Mentor Meeting slots.");
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không thể tải lịch trống của Mentor.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorMeetingPurchaseResponse>> PurchaseAsync(
        PurchaseMentorPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.SubscriptionPlanId <= 0)
            return ApiResponse<MentorMeetingPurchaseResponse>.Fail("SubscriptionPlanId phải lớn hơn 0.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Learner)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Chỉ Learner mới có thể mua gói meeting.", HttpStatusCode.Forbidden);

            var plan = await unitOfWork.SubscriptionPlans.GetMeetingPlanAsync(request.SubscriptionPlanId, cancellationToken);
            if (plan is null)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Không tìm thấy gói meeting đang hoạt động.", HttpStatusCode.NotFound);
            if (plan.MeetingSessionCount is not (1 or 3))
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Chỉ hỗ trợ gói 1 hoặc 3 buổi.");
            if (plan.Price <= 0 || plan.Price != decimal.Truncate(plan.Price) || plan.Price > int.MaxValue)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Giá gói phải là số tiền VND nguyên dương.");

            var now = DateTime.UtcNow;
            await ExpirePendingPaymentsAsync(cancellationToken);
            var expiresAt = now.AddMinutes(15);
            var subscription = new UserSubscription
            {
                UserId = claims.Id,
                SubscriptionPlanId = plan.Id,
                SubscriptionPlan = plan,
                MentorId = null,
                AmountPaid = plan.Price,
                PaymentStatus = PaymentStatus.Pending,
                MeetingSessionsIncluded = plan.MeetingSessionCount,
                MeetingSessionsUsed = 0,
                PaymentExpiresAt = expiresAt,
                PurchasedAt = now,
                AiGradingQuota = plan.AiGradingQuota
            };
            await unitOfWork.UserSubscriptions.AddAsync(subscription, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            PaymentLinkResult checkout;
            try
            {
                var description = $"Mentor plan {subscription.Id}";
                if (description.Length > 25) description = description[..25];
                var payOs = appSettings.PayOs;
                if (payOs is null || string.IsNullOrWhiteSpace(payOs.WebReturnUrl) || string.IsNullOrWhiteSpace(payOs.WebCancelUrl))
                    throw new InvalidOperationException("Thiếu cấu hình PayOS return/cancel URL.");

                checkout = await paymentGateway.CreatePaymentLinkAsync(
                    subscription.Id,
                    decimal.ToInt32(plan.Price),
                    description,
                    Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(payOs.WebReturnUrl, "subscriptionId", subscription.Id.ToString()),
                    Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(payOs.WebCancelUrl, "subscriptionId", subscription.Id.ToString()),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PayOS checkout creation failed for subscription {SubscriptionId}.", subscription.Id);
                subscription.PaymentStatus = PaymentStatus.Failed;
                subscription.PaymentExpiresAt = null;
                unitOfWork.UserSubscriptions.Update(subscription);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Không thể tạo liên kết thanh toán. Chưa cộng hoặc trừ lượt.", HttpStatusCode.BadGateway);
            }

            subscription.PaymentTransactionId = checkout.PaymentLinkId;
            unitOfWork.UserSubscriptions.Update(subscription);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<MentorMeetingPurchaseResponse>.Success(new MentorMeetingPurchaseResponse
            {
                SubscriptionId = subscription.Id,
                BookingId = null,
                OrderCode = subscription.Id,
                PlanName = plan.Name,
                MeetingSessionCount = plan.MeetingSessionCount,
                Features = plan.Features.Where(x => !x.IsDeleted).Select(MapFeature).ToList(),
                Amount = plan.Price,
                CheckoutUrl = checkout.CheckoutUrl,
                PaymentLinkId = checkout.PaymentLinkId,
                QrCode = checkout.QrCode,
                PaymentExpiresAt = expiresAt
            }, HttpStatusCode.Created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create a Mentor Meeting purchase.");
            return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Không thể tạo giao dịch mua gói meeting.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorBookingResponse>> BookIncludedSessionAsync(
        int subscriptionId, BookIncludedMeetingRequest request, CancellationToken cancellationToken)
    {
        if (subscriptionId <= 0 || request.MentorAvailabilityId <= 0)
            return ApiResponse<MentorBookingResponse>.Fail("SubscriptionId và MentorAvailabilityId phải lớn hơn 0.");

        if (request.Question?.Length > 2000)
            return ApiResponse<MentorBookingResponse>.Fail("Câu hỏi tối đa 2000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Learner)
                return ApiResponse<MentorBookingResponse>.Fail("Chỉ Learner mới có thể đặt buổi meeting.", HttpStatusCode.Forbidden);

            await ExpirePendingPaymentsAsync(cancellationToken);
            var subscription = await unitOfWork.UserSubscriptions
                .GetMeetingSubscriptionWithBookingsAsync(subscriptionId, claims.Id, cancellationToken);
            if (subscription is null)
                return ApiResponse<MentorBookingResponse>.Fail("Không tìm thấy gói meeting.", HttpStatusCode.NotFound);
            if (subscription.PaymentStatus != PaymentStatus.Paid)
                return ApiResponse<MentorBookingResponse>.Fail("Gói chưa thanh toán thành công.", HttpStatusCode.Conflict);
            if (subscription.StartDate > DateTime.UtcNow)
                return ApiResponse<MentorBookingResponse>.Fail("Gói chưa có hiệu lực.", HttpStatusCode.Conflict);
            if (subscription.EndDate is DateTime endDate && endDate < DateTime.UtcNow)
                return ApiResponse<MentorBookingResponse>.Fail("Gói meeting đã hết hạn.", HttpStatusCode.Conflict);
            if (subscription.MeetingSessionsUsed >= subscription.MeetingSessionsIncluded)
                return ApiResponse<MentorBookingResponse>.Fail("Gói đã sử dụng hết số buổi meeting.", HttpStatusCode.Conflict);

            var slot = await unitOfWork.MentorAvailabilities.GetAvailableSlotByIdAsync(
                request.MentorAvailabilityId, DateTime.UtcNow, cancellationToken);
            if (slot is null || slot.CancelRequestedAt is not null)
                return ApiResponse<MentorBookingResponse>.Fail("Slot không còn trống hoặc không hợp lệ.", HttpStatusCode.Conflict);
            if (subscription.EndDate is DateTime expiry && slot.EndTime > expiry)
                return ApiResponse<MentorBookingResponse>.Fail("Tiết phải kết thúc trước khi gói hết hạn.");
            slot.BookedCount++;

            subscription.MeetingSessionsUsed++;
            var booking = CreateConfirmedBooking(subscription, slot);
            booking.Question = request.Question?.Trim();
            unitOfWork.MentorAvailabilities.Update(slot);
            unitOfWork.UserSubscriptions.Update(subscription);
            await unitOfWork.MentorBookings.AddAsync(booking, cancellationToken);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Included session slot {SlotId} was reserved concurrently.", request.MentorAvailabilityId);
                return ApiResponse<MentorBookingResponse>.Fail("Slot vừa được người khác đặt. Vui lòng chọn slot khác.", HttpStatusCode.Conflict);
            }

            return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claims.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not book an included Mentor Meeting session.");
            return ApiResponse<MentorBookingResponse>.Fail("Không thể đặt buổi meeting.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorMeetingPurchaseStatusResponse>>> GetMyPurchasesAsync(CancellationToken ct)
    {
        var user = claimService.GetUserClaim();
        if (user.Role != Role.Learner)
            return ApiResponse<List<MentorMeetingPurchaseStatusResponse>>.Fail("Chỉ học viên được xem lượt của mình.", HttpStatusCode.Forbidden);
        var ids = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Where(x => x.UserId == user.Id && !x.IsDeleted && x.MeetingSessionsIncluded > 0)
            .OrderByDescending(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var result = new List<MentorMeetingPurchaseStatusResponse>();
        foreach (var id in ids)
        {
            var purchase = await GetPurchaseStatusAsync(id, ct);
            if (!purchase.IsSuccess) return ApiResponse<List<MentorMeetingPurchaseStatusResponse>>.Fail("Không tải được số lượt.");
            result.Add(purchase.Result!);
        }
        return ApiResponse<List<MentorMeetingPurchaseStatusResponse>>.Success(result);
    }

    public async Task<ApiResponse<MentorBookingResponse>> BookFromBalanceAsync(BookIncludedMeetingRequest request, CancellationToken ct)
    {
        var user = claimService.GetUserClaim();
        if (user.Role != Role.Learner)
            return ApiResponse<MentorBookingResponse>.Fail("Chỉ học viên được đặt lịch.", HttpStatusCode.Forbidden);
        var slot = await unitOfWork.MentorAvailabilities.GetAvailableSlotByIdAsync(request.MentorAvailabilityId, DateTime.UtcNow, ct);
        if (slot is null) return ApiResponse<MentorBookingResponse>.Fail("Tiết không còn trống.", HttpStatusCode.Conflict);
        var now = DateTime.UtcNow;
        var subscriptionId = await unitOfWork.UserSubscriptions.Query()
            .Where(x => x.UserId == user.Id && !x.IsDeleted && x.PaymentStatus == PaymentStatus.Paid
                && x.MeetingSessionsIncluded > x.MeetingSessionsUsed && (x.StartDate == null || x.StartDate <= now)
                && (x.EndDate == null || x.EndDate >= slot.EndTime))
            .OrderBy(x => x.EndDate == null).ThenBy(x => x.EndDate).ThenBy(x => x.Id)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        if (subscriptionId is null) return ApiResponse<MentorBookingResponse>.Fail("Không đủ lượt còn hạn cho tiết này. Vui lòng mua gói trước.", HttpStatusCode.Conflict);
        return await BookIncludedSessionAsync(subscriptionId.Value, request, ct);
    }

    public async Task<ApiResponse<List<MentorBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var learnerId = claimService.GetUserClaim().Id;
            var bookings = await unitOfWork.MentorBookings.GetByLearnerAsync(learnerId, cancellationToken);
            return ApiResponse<List<MentorBookingResponse>>.Success(bookings.Select(x => MapBooking(x, learnerId)).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load learner Mentor Meeting bookings.");
            return ApiResponse<List<MentorBookingResponse>>.Fail("Không thể tải lịch meeting của bạn.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> GetPurchaseStatusAsync(
        int subscriptionId, CancellationToken cancellationToken)
    {
        if (subscriptionId <= 0)
            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("SubscriptionId phải lớn hơn 0.");
        try
        {
            var learnerId = claimService.GetUserClaim().Id;
            var subscription = await unitOfWork.UserSubscriptions
                .GetMeetingSubscriptionWithBookingsAsync(subscriptionId, learnerId, cancellationToken);
            if (subscription is null)
                return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Không tìm thấy giao dịch.", HttpStatusCode.NotFound);

            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Success(new MentorMeetingPurchaseStatusResponse
            {
                SubscriptionId = subscription.Id,
                PlanName = subscription.SubscriptionPlan.Name,
                PaymentStatus = subscription.PaymentStatus,
                IsCheckoutExpired = subscription.PaymentStatus == PaymentStatus.Pending
                    && subscription.PaymentExpiresAt is DateTime expiresAt && expiresAt <= DateTime.UtcNow,
                AmountPaid = subscription.AmountPaid,
                PaymentExpiresAt = subscription.PaymentExpiresAt,
                EndDate = subscription.EndDate,
                MentorId = subscription.MentorId,
                MeetingSessionsIncluded = subscription.MeetingSessionsIncluded,
                MeetingSessionsUsed = subscription.MeetingSessionsUsed,
                Bookings = subscription.MentorBookings.Select(x => MapBooking(x, learnerId)).ToList(),
                TransactionId = subscription.PaymentTransactionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load Mentor Meeting purchase status.");
            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Không thể tải trạng thái giao dịch.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorMeetingPurchaseStatusResponse>> SyncPurchaseAsync(int subscriptionId, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Learner)
            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Chỉ học viên được kiểm tra đơn mua của mình.", HttpStatusCode.Forbidden);
        var subscription = await unitOfWork.UserSubscriptions.GetMeetingSubscriptionWithBookingsAsync(subscriptionId, claims.Id, cancellationToken);
        if (subscription is null)
            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Không tìm thấy giao dịch.", HttpStatusCode.NotFound);
        if (subscription.PaymentStatus == PaymentStatus.Pending && !string.IsNullOrWhiteSpace(subscription.PaymentTransactionId))
        {
            try
            {
                var information = await paymentGateway.GetPaymentInformationAsync(subscription.Id, cancellationToken);
                if (information.OrderCode != subscription.Id || information.Amount != subscription.AmountPaid
                    || information.PaymentLinkId != subscription.PaymentTransactionId)
                    return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Thông tin giao dịch PayOS không khớp đơn mua.", HttpStatusCode.Conflict);
                if (information.Status == "PAID" && information.AmountPaid >= subscription.AmountPaid)
                {
                    MentorPurchaseFinalizer.MarkPaid(subscription, appSettings, null);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else if (information.Status == "CANCELLED")
                {
                    subscription.PaymentStatus = PaymentStatus.Failed;
                    subscription.PaymentExpiresAt = null;
                    foreach (var booking in subscription.MentorBookings.Where(b => b.Status == MentorStatus.AwaitingPayment))
                    {
                        booking.Status = MentorStatus.Cancelled;
                        if (booking.MentorAvailability is {} heldSlot) heldSlot.BookedCount = Math.Max(0, heldSlot.BookedCount - 1);
                        subscription.MeetingSessionsUsed = Math.Max(0, subscription.MeetingSessionsUsed - 1);
                        booking.MentorAvailability = null;
                        booking.MentorAvailabilityId = null;
                    }
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not reconcile Mentor purchase {SubscriptionId}.", subscriptionId);
                return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Chưa kiểm tra được PayOS. Vui lòng thử lại; hệ thống chưa xác nhận thanh toán.", HttpStatusCode.BadGateway);
            }
        }
        return await GetPurchaseStatusAsync(subscriptionId, cancellationToken);
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Mentor mới có thể xem lịch của mình.", HttpStatusCode.Forbidden);
            var slots = await unitOfWork.MentorAvailabilities.GetByMentorAsync(claims.Id);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.Select(MapAvailability).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load Mentor availability.");
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không thể tải lịch trống của bạn.", HttpStatusCode.InternalServerError);
        }
    }

    public Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(CreateMentorAvailabilityRequest request, CancellationToken cancellationToken)
        => Task.FromResult(ApiResponse<List<MentorAvailabilityResponse>>.Fail("Hãy đăng ký buổi rảnh tuần sau; admin sẽ xếp các tiết 50 phút."));

    public Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(ApiResponse<bool>.Fail("Tiết do admin xếp. Hãy gửi yêu cầu điều chỉnh."));

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> RequestCancelAvailabilitiesAsync(
        CancelAvailabilitiesRequest request, CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(request.Ids);
        if (ids.Count == 0 || ids.Count > MaxBulkSlots || ids.Any(x => x <= 0))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail($"Danh sách slot không hợp lệ (1-{MaxBulkSlots} slot).");
        if (request.Reason?.Length > 1000)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Lý do hủy tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Mentor mới có thể hủy lịch của mình.", HttpStatusCode.Forbidden);

            await ExpirePendingPaymentsAsync(cancellationToken);
            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Include(x => x.Mentor)
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.MentorId == claims.Id)
                .ToListAsync(cancellationToken);
            if (slots.Count != ids.Count)
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound);

            var now = DateTime.UtcNow;
            if (slots.Any(x => x.CancelRequestedAt is not null))
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có slot đang chờ admin duyệt hủy.", HttpStatusCode.Conflict);
            if (slots.Any(x => x.StartTime - now < TimeSpan.FromDays(MinCancelAdvanceDays)))
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail(
                    $"Chỉ được yêu cầu hủy trước giờ bắt đầu ít nhất {MinCancelAdvanceDays} ngày.", HttpStatusCode.Conflict);
            if ((await GetSlotIdsWithActiveBookingsAsync(slots, cancellationToken)).Count > 0)
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có slot đã có người đặt, không thể hủy.", HttpStatusCode.Conflict);

            foreach (var slot in slots)
            {
                slot.CancelRequestedAt = now;
                slot.CancelReason = request.Reason?.Trim();
                slot.CancelRejectedAt = null;
                slot.CancelRejectionReason = null;
                slot.ModifiedDate = now;
                unitOfWork.MentorAvailabilities.Update(slot);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.OrderBy(x => x.StartTime).Select(MapAvailability).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not request cancel for Mentor availabilities.");
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không thể gửi yêu cầu hủy.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Admin duyệt (xóa slot) hoặc từ chối (giữ slot) yêu cầu hủy của Mentor, cho một hoặc nhiều slot.</summary>
    public async Task<ApiResponse<bool>> ReviewCancelAvailabilitiesAsync(
        List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken)
    {
        var distinctIds = NormalizeIds(ids);
        if (distinctIds.Count == 0 || distinctIds.Count > MaxBulkSlots || distinctIds.Any(x => x <= 0))
            return ApiResponse<bool>.Fail($"Danh sách slot không hợp lệ (1-{MaxBulkSlots} slot).");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Admin)
                return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt yêu cầu hủy.", HttpStatusCode.Forbidden);

            await ExpirePendingPaymentsAsync(cancellationToken);
            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            if (slots.Count != distinctIds.Count)
                return ApiResponse<bool>.Fail("Có slot không tồn tại.", HttpStatusCode.NotFound);
            if (slots.Any(x => x.CancelRequestedAt is null))
                return ApiResponse<bool>.Fail("Có slot không có yêu cầu hủy.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            if (approve)
            {
                // Re-check: trong lúc chờ duyệt có thể đã có người đặt.
                if ((await GetSlotIdsWithActiveBookingsAsync(slots, cancellationToken)).Count > 0)
                    return ApiResponse<bool>.Fail("Có slot đã có người đặt, không thể duyệt hủy.", HttpStatusCode.Conflict);
            }

            foreach (var slot in slots)
            {
                if (approve)
                {
                    slot.IsDeleted = true;
                }
                else
                {
                    slot.CancelRequestedAt = null;
                    slot.CancelRejectedAt = now;
                    slot.CancelRejectionReason = rejectionReason?.Trim();
                }
                slot.ModifiedDate = now;
                unitOfWork.MentorAvailabilities.Update(slot);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Success(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not review cancel requests for Mentor availabilities.");
            return ApiResponse<bool>.Fail("Không thể xử lý yêu cầu hủy.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorBookingResponse>>> GetMyMeetingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<List<MentorBookingResponse>>.Fail("Chỉ Mentor mới có thể xem lịch meeting.", HttpStatusCode.Forbidden);
            var bookings = await unitOfWork.MentorBookings.GetPaidBookingsForMentorAsync(claims.Id, cancellationToken);
            return ApiResponse<List<MentorBookingResponse>>.Success(bookings.Select(x => MapBooking(x, claims.Id)).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load Mentor bookings.");
            return ApiResponse<List<MentorBookingResponse>>.Fail("Không thể tải lịch meeting của Mentor.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorBookingResponse>> GetMentorBookingAsync(int bookingId, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<MentorBookingResponse>.Fail("Chỉ Mentor mới được xem yêu cầu này.", HttpStatusCode.Forbidden);
        var booking = await unitOfWork.MentorBookings.GetForChatAsync(bookingId, cancellationToken);
        if (booking is null || booking.MentorAvailability?.MentorId != claims.Id
            || booking.UserSubscription.PaymentStatus != PaymentStatus.Paid)
            return ApiResponse<MentorBookingResponse>.Fail("Không tìm thấy yêu cầu tư vấn.", HttpStatusCode.NotFound);
        return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claims.Id));
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetPendingAvailabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Admin)
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Admin mới xem được danh sách chờ duyệt.", HttpStatusCode.Forbidden);

            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Include(x => x.Mentor)
                .Where(x => !x.IsDeleted && !x.IsApproved && x.RejectedAt == null && x.StartTime > DateTime.UtcNow)
                .OrderBy(x => x.MentorId).ThenBy(x => x.StartTime)
                .ToListAsync(cancellationToken);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.Select(MapAvailability).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load pending Mentor availabilities.");
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không thể tải danh sách lịch chờ duyệt.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Admin duyệt hoặc từ chối một hoặc nhiều slot mới của Mentor (giao diện gom các slot cùng buổi rồi gửi danh sách id).</summary>
    public async Task<ApiResponse<bool>> ReviewAvailabilitiesAsync(
        List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken)
    {
        var distinctIds = NormalizeIds(ids);
        if (distinctIds.Count == 0 || distinctIds.Count > MaxBulkSlots || distinctIds.Any(x => x <= 0))
            return ApiResponse<bool>.Fail($"Danh sách slot không hợp lệ (1-{MaxBulkSlots} slot).");
        if (!approve && string.IsNullOrWhiteSpace(rejectionReason))
            return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối.");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Admin)
                return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt lịch.", HttpStatusCode.Forbidden);

            var user = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
            var adminName = user is not null ? $"{user.FirstName} {user.LastName}".Trim() : null;

            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            if (slots.Count != distinctIds.Count)
                return ApiResponse<bool>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound);
            if (slots.Any(x => x.IsApproved || x.RejectedAt is not null))
                return ApiResponse<bool>.Fail("Có lịch đã được xử lý.", HttpStatusCode.Conflict);
            if (slots.Any(x => x.StartTime <= now))
                return ApiResponse<bool>.Fail("Có lịch đã quá hạn, không thể duyệt.", HttpStatusCode.Conflict);

            foreach (var slot in slots)
            {
                slot.IsApproved = approve;
                slot.RejectedAt = approve ? null : now;
                slot.RejectionReason = approve ? null : rejectionReason!.Trim();
                slot.AdminName = adminName;
                slot.ModifiedDate = now;
                unitOfWork.MentorAvailabilities.Update(slot);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Success(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not review Mentor availabilities.");
            return ApiResponse<bool>.Fail("Không thể xử lý duyệt lịch.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorBookingResponse>> AcceptMentorBookingAsync(int bookingId, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<MentorBookingResponse>.Fail("Chỉ Mentor mới được tiếp nhận yêu cầu.", HttpStatusCode.Forbidden);
        var booking = await unitOfWork.MentorBookings.Query()
            .Include(x => x.UserSubscription).ThenInclude(x => x.UserAccount)
            .Include(x => x.MentorAvailability).ThenInclude(x => x!.Mentor)
            .FirstOrDefaultAsync(x => x.Id == bookingId && !x.IsDeleted, cancellationToken);
        if (booking is null || booking.MentorAvailability?.MentorId != claims.Id
            || booking.UserSubscription.PaymentStatus != PaymentStatus.Paid)
            return ApiResponse<MentorBookingResponse>.Fail("Không tìm thấy yêu cầu tư vấn.", HttpStatusCode.NotFound);
        if (booking.Status == MentorStatus.Accepted)
            return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claims.Id));
        if (booking.Status != MentorStatus.Assigned)
            return ApiResponse<MentorBookingResponse>.Fail("Yêu cầu không còn ở trạng thái chờ tiếp nhận.", HttpStatusCode.Conflict);
        var slot = booking.MentorAvailability;
        if (slot.IsDeleted || !slot.IsApproved || slot.RejectedAt != null || slot.EndTime <= DateTime.UtcNow)
            return ApiResponse<MentorBookingResponse>.Fail("Lịch chưa được duyệt, đã đóng hoặc hết hạn.", HttpStatusCode.Conflict);
        booking.Status = MentorStatus.Accepted;
        booking.ModifiedDate = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claims.Id));
    }

    private static List<int> NormalizeIds(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    /// <summary>
    /// Trả về id các slot "đã có người đặt": BookedCount > 0 hoặc còn booking chưa hủy
    /// (kể cả booking đang giữ chỗ chờ thanh toán). Truy vấn một lần cho cả danh sách slot.
    /// </summary>
    private async Task<HashSet<int>> GetSlotIdsWithActiveBookingsAsync(
        IReadOnlyCollection<MentorAvailability> slots, CancellationToken cancellationToken)
    {
        var result = slots.Where(x => x.BookedCount > 0).Select(x => x.Id).ToHashSet();
        var slotIds = slots.Select(x => x.Id).ToList();
        var bookedIds = await unitOfWork.MentorBookings.Query()
            .Where(b => b.MentorAvailabilityId != null
                && slotIds.Contains(b.MentorAvailabilityId.Value)
                && !b.IsDeleted
                && b.Status != MentorStatus.Cancelled)
            .Select(b => b.MentorAvailabilityId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        result.UnionWith(bookedIds);
        return result;
    }

    private async Task ExpirePendingPaymentsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await unitOfWork.MentorBookings.GetExpiredPaymentHoldsAsync(now, cancellationToken);
        if (expired.Count == 0) return;
        foreach (var booking in expired)
        {
            booking.Status = MentorStatus.Cancelled;
            if (booking.MentorAvailability is not null)
            {
                var slot = booking.MentorAvailability;
                slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
                unitOfWork.MentorAvailabilities.Update(slot);
                booking.MentorAvailability = null;
                booking.MentorAvailabilityId = null;
            }
            booking.UserSubscription.MeetingSessionsUsed = Math.Max(0, booking.UserSubscription.MeetingSessionsUsed - 1);
            booking.UserSubscription.PaymentExpiresAt = now;
            unitOfWork.UserSubscriptions.Update(booking.UserSubscription);
            unitOfWork.MentorBookings.Update(booking);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private MentorBooking CreateConfirmedBooking(UserSubscription subscription, MentorAvailability slot)
    {
        return new MentorBooking
        {
            UserSubscription = subscription,
            MentorAvailability = slot,
            Status = MentorStatus.Assigned,
            ScheduledAt = slot.StartTime,
            MeetingLink = $"{appSettings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{slot.RoomId:D}/"
        };
    }

    private static MentorMeetingPlanResponse MapPlan(SubscriptionPlan plan) => new()
    {
        IsActive = plan.IsActive,
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        DurationDays = plan.MeetingSessionCount switch { 1 => 14, 3 => 35, _ => plan.DurationDays },
        Price = plan.Price,
        MeetingSessionCount = plan.MeetingSessionCount,
        Features = plan.Features.Where(x => !x.IsDeleted).Select(MapFeature).ToList()
    };

    private static MentorMeetingFeatureResponse MapFeature(Feature feature) => new()
    {
        Id = feature.Id,
        Name = feature.Name,
        Description = feature.Description
    };

    private static MentorAvailabilityResponse MapAvailability(MentorAvailability slot) => new()
    {
        Id = slot.Id,
        MentorId = slot.MentorId,
        MentorName = $"{slot.Mentor?.FirstName} {slot.Mentor?.LastName}".Trim(),
        StartTime = slot.StartTime,
        EndTime = slot.EndTime,
        IsBooked = slot.IsFull,
        IsApproved = slot.IsApproved,
        RejectedAt = slot.RejectedAt,
        RejectionReason = slot.RejectionReason,
        BookedCount = slot.BookedCount,
        MaxLearners = slot.MaxLearners,
        AdminName = slot.AdminName,

        // Tiêu chí để admin duyệt lịch mới: đăng ký trước giờ bắt đầu bao lâu.
        AdvanceDaysAtCreation = slot.CreatedDate is DateTime createdAt ? (int)(slot.StartTime - createdAt).TotalDays : 0,
        MeetsAdvanceRule = slot.CreatedDate is DateTime created && slot.StartTime >= created.AddMonths(MinAdvanceMonths),

        // Luồng yêu cầu hủy.
        CancelRequestedAt = slot.CancelRequestedAt,
        CancelReason = slot.CancelReason,
        CancelRejectedAt = slot.CancelRejectedAt,
        CancelRejectionReason = slot.CancelRejectionReason,
        MentorWeeklyAvailabilityId = slot.MentorWeeklyAvailabilityId, PublishedAt = slot.PublishedAt,
        ChangeRequestReason = slot.ChangeRequestReason, ChangeRequestedAt = slot.ChangeRequestedAt,
        ChangeResolution = slot.ChangeResolution,
    };

    private static MentorBookingResponse MapBooking(MentorBooking booking, int currentUserId)
    {
        var isMentor = booking.MentorAvailability?.MentorId == currentUserId;
        var learner = booking.UserSubscription?.UserAccount;
        var mentor = booking.MentorAvailability?.Mentor;
        return new MentorBookingResponse
        {
            Id = booking.Id,
            UserSubscriptionId = booking.UserSubscriptionId,
            MentorAvailabilityId = booking.MentorAvailabilityId,
            MentorId = booking.MentorAvailability?.MentorId,
            MentorName = mentor is null ? null : $"{mentor.FirstName} {mentor.LastName}".Trim(),
            LearnerId = isMentor ? booking.UserSubscription?.UserId : null,
            LearnerName = isMentor && learner is not null ? $"{learner.FirstName} {learner.LastName}".Trim() : null,
            Status = booking.Status,
            PaymentStatus = booking.UserSubscription?.PaymentStatus,
            ScheduledAt = booking.ScheduledAt,
            MeetingLink = booking.MeetingLink,
            RoomId = booking.MentorAvailability?.RoomId,
            Question = booking.Question,
            EndsAt = booking.MentorAvailability?.EndTime,
            CreatedAt = booking.CreatedDate,
        };
    }
}