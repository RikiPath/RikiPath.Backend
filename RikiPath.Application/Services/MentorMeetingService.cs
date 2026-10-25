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
        if (request.SubscriptionPlanId <= 0 || request.MentorAvailabilityId <= 0)
            return ApiResponse<MentorMeetingPurchaseResponse>.Fail("SubscriptionPlanId và MentorAvailabilityId phải lớn hơn 0.");
        if (request.Question?.Length > 2000)
            return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Câu hỏi tối đa 2000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Learner)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Chỉ Learner mới có thể mua gói meeting.", HttpStatusCode.Forbidden);

            var plan = await unitOfWork.SubscriptionPlans.GetMeetingPlanAsync(request.SubscriptionPlanId, cancellationToken);
            if (plan is null)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Không tìm thấy gói meeting đang hoạt động.", HttpStatusCode.NotFound);
            if (plan.Price <= 0 || plan.Price != decimal.Truncate(plan.Price) || plan.Price > int.MaxValue)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Giá gói phải là số tiền VND nguyên dương.");

            var now = DateTime.UtcNow;
            await ExpirePendingPaymentsAsync(cancellationToken);
            var slot = await unitOfWork.MentorAvailabilities.GetAvailableSlotByIdAsync(request.MentorAvailabilityId, now, cancellationToken);
            if (slot is null || slot.CancelRequestedAt is not null)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Slot không còn trống hoặc không hợp lệ.", HttpStatusCode.Conflict);

            var expiresAt = now.AddMinutes(15);
            var subscription = new UserSubscription
            {
                UserId = claims.Id,
                SubscriptionPlanId = plan.Id,
                SubscriptionPlan = plan,
                MentorId = slot.MentorId,
                AmountPaid = plan.Price,
                PaymentStatus = PaymentStatus.Pending,
                MeetingSessionsIncluded = plan.MeetingSessionCount,
                MeetingSessionsUsed = 1,
                PaymentExpiresAt = expiresAt,
                PurchasedAt = now,
                AiGradingQuota = plan.AiGradingQuota
            };
            var booking = new MentorBooking
            {
                UserSubscription = subscription,
                MentorAvailability = slot,
                Status = MentorStatus.AwaitingPayment,
                Question = request.Question?.Trim(),
                ScheduledAt = slot.StartTime
            };

            unitOfWork.MentorAvailabilities.Update(slot);
            await unitOfWork.UserSubscriptions.AddAsync(subscription, cancellationToken);
            await unitOfWork.MentorBookings.AddAsync(booking, cancellationToken);
            try
            {
                // One SaveChanges persists the slot hold, subscription, and booking atomically.
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Mentor slot {SlotId} was reserved concurrently.", request.MentorAvailabilityId);
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Slot vừa được người khác đặt. Vui lòng chọn slot khác.", HttpStatusCode.Conflict);
            }

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
                booking.Status = MentorStatus.Cancelled;
                booking.MentorAvailability = null;
                booking.MentorAvailabilityId = null;
                unitOfWork.UserSubscriptions.Update(subscription);
                unitOfWork.MentorBookings.Update(booking);
                unitOfWork.MentorAvailabilities.Update(slot);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Không thể tạo liên kết thanh toán. Slot đã được giải phóng.", HttpStatusCode.BadGateway);
            }

            subscription.PaymentTransactionId = checkout.PaymentLinkId;
            unitOfWork.UserSubscriptions.Update(subscription);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<MentorMeetingPurchaseResponse>.Success(new MentorMeetingPurchaseResponse
            {
                SubscriptionId = subscription.Id,
                BookingId = booking.Id,
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
            if (subscription.EndDate is DateTime endDate && endDate < DateTime.UtcNow)
                return ApiResponse<MentorBookingResponse>.Fail("Gói meeting đã hết hạn.", HttpStatusCode.Conflict);
            if (subscription.MeetingSessionsUsed >= subscription.MeetingSessionsIncluded)
                return ApiResponse<MentorBookingResponse>.Fail("Gói đã sử dụng hết số buổi meeting.", HttpStatusCode.Conflict);

            var slot = await unitOfWork.MentorAvailabilities.GetAvailableSlotByIdAsync(
                request.MentorAvailabilityId, DateTime.UtcNow, cancellationToken);
            if (slot is null || slot.CancelRequestedAt is not null)
                return ApiResponse<MentorBookingResponse>.Fail("Slot không còn trống hoặc không hợp lệ.", HttpStatusCode.Conflict);

            // A purchased meeting package is tied to its original Mentor. To book a different
            // Mentor, the learner must purchase a separate package.
            var existingMentorIds = (subscription.MentorBookings ?? [])
                .Where(x => !x.IsDeleted && x.MentorAvailability is not null)
                .Select(x => x.MentorAvailability!.MentorId)
                .Distinct()
                .ToList();
            var packageMentorId = subscription.MentorId ?? existingMentorIds.SingleOrDefault();
            if (existingMentorIds.Count > 1 || (packageMentorId > 0 && packageMentorId != slot.MentorId))
                return ApiResponse<MentorBookingResponse>.Fail(
                    "Gói meeting này chỉ dùng được với Mentor đã chọn ban đầu. Muốn đặt Mentor khác, vui lòng mua gói riêng.",
                    HttpStatusCode.Conflict);
            subscription.MentorId = packageMentorId > 0 ? packageMentorId : slot.MentorId;

            subscription.MeetingSessionsUsed++;
            var booking = CreateConfirmedBooking(subscription, slot);
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

    public async Task<ApiResponse<MentorAvailabilityResponse>> CreateAvailabilityAsync(
        CreateMentorAvailabilityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ Mentor mới có thể tạo lịch trống.", HttpStatusCode.Forbidden);

            var now = DateTime.UtcNow;
            var start = request.StartTime.ToUniversalTime();
            var end = request.EndTime.ToUniversalTime();

            if (end <= start)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Thời gian bắt đầu phải trước thời gian kết thúc.");
            if (start < now.AddMonths(MinAdvanceMonths))
                return ApiResponse<MentorAvailabilityResponse>.Fail(
                    $"Lịch trống phải được đăng ký trước ít nhất {MinAdvanceMonths} tháng so với thời điểm bắt đầu.");
            if (end - start > MaxSlotDuration)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Một slot không được dài quá 4 giờ.");
            if (await unitOfWork.MentorAvailabilities.HasOverlappingSlotAsync(claims.Id, start, end, cancellationToken))
                return ApiResponse<MentorAvailabilityResponse>.Fail("Slot bị trùng với lịch đã tạo.", HttpStatusCode.Conflict);

            var slot = new MentorAvailability
            {
                MentorId = claims.Id,
                StartTime = start,
                EndTime = end,
                IsApproved = false
            };
            await unitOfWork.MentorAvailabilities.AddAsync(slot, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            slot.Mentor = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken) ?? null!;
            return ApiResponse<MentorAvailabilityResponse>.Success(MapAvailability(slot), HttpStatusCode.Created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create Mentor availability.");
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể tạo khung giờ trống.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Mentor gửi yêu cầu hủy lịch trống. Chỉ được gửi khi slot chưa có ai đặt và còn cách giờ bắt đầu
    /// ít nhất <see cref="MinCancelAdvanceDays"/> ngày. Slot chỉ thực sự bị hủy sau khi Admin duyệt.
    /// </summary>
    public async Task<ApiResponse<MentorAvailabilityResponse>> RequestCancelAvailabilityAsync(
        int id, CancelAvailabilityRequest request, CancellationToken cancellationToken)
    {
        if (id <= 0) return ApiResponse<MentorAvailabilityResponse>.Fail("AvailabilityId phải lớn hơn 0.");
        if (request.Reason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do hủy tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ Mentor mới có thể hủy lịch của mình.", HttpStatusCode.Forbidden);

            await ExpirePendingPaymentsAsync(cancellationToken);
            var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
            if (slot is null || slot.IsDeleted || slot.MentorId != claims.Id)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy khung giờ.", HttpStatusCode.NotFound);
            if (slot.CancelRequestedAt is not null)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Yêu cầu hủy đang chờ admin duyệt.", HttpStatusCode.Conflict);
            if (await HasActiveBookingAsync(slot, cancellationToken))
                return ApiResponse<MentorAvailabilityResponse>.Fail("Slot đã có người đặt, không thể hủy.", HttpStatusCode.Conflict);
            if (slot.StartTime - DateTime.UtcNow < TimeSpan.FromDays(MinCancelAdvanceDays))
                return ApiResponse<MentorAvailabilityResponse>.Fail(
                    $"Chỉ được yêu cầu hủy trước giờ bắt đầu ít nhất {MinCancelAdvanceDays} ngày.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            slot.CancelRequestedAt = now;
            slot.CancelReason = request.Reason?.Trim();
            slot.CancelRejectedAt = null;
            slot.CancelRejectionReason = null;
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<MentorAvailabilityResponse>.Success(MapAvailability(slot));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not request cancel for Mentor availability {AvailabilityId}.", id);
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể gửi yêu cầu hủy.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>Admin duyệt (xóa slot) hoặc từ chối (giữ slot) yêu cầu hủy của Mentor.</summary>
    public async Task<ApiResponse<bool>> ReviewCancelAvailabilityAsync(
        int id, bool approve, string? rejectionReason, CancellationToken cancellationToken)
    {
        if (id <= 0) return ApiResponse<bool>.Fail("AvailabilityId phải lớn hơn 0.");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Admin)
                return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt yêu cầu hủy.", HttpStatusCode.Forbidden);

            await ExpirePendingPaymentsAsync(cancellationToken);
            var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
            if (slot is null || slot.IsDeleted || slot.CancelRequestedAt is null)
                return ApiResponse<bool>.Fail("Không có yêu cầu hủy cho slot này.", HttpStatusCode.NotFound);

            var now = DateTime.UtcNow;
            if (approve)
            {
                // Re-check: trong lúc chờ duyệt có thể đã có người đặt.
                if (await HasActiveBookingAsync(slot, cancellationToken))
                    return ApiResponse<bool>.Fail("Slot đã có người đặt, không thể duyệt hủy.", HttpStatusCode.Conflict);
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
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Success(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not review cancel request for Mentor availability {AvailabilityId}.", id);
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
                .OrderBy(x => x.CreatedDate)
                .ToListAsync(cancellationToken);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.Select(MapAvailability).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load pending Mentor availabilities.");
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không thể tải danh sách lịch chờ duyệt.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorAvailabilityResponse>> ReviewAvailabilityAsync(
        int id, bool approve, string? rejectionReason, CancellationToken cancellationToken)
    {
        if (id <= 0) return ApiResponse<MentorAvailabilityResponse>.Fail("AvailabilityId phải lớn hơn 0.");
        if (!approve && string.IsNullOrWhiteSpace(rejectionReason))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Vui lòng nhập lý do từ chối.");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Admin)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ Admin mới được duyệt lịch.", HttpStatusCode.Forbidden);

            var user = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
            var slot = await unitOfWork.MentorAvailabilities.Query()
                .Include(x => x.Mentor)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (slot is null)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy khung giờ.", HttpStatusCode.NotFound);
            if (slot.IsApproved || slot.RejectedAt is not null)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Lịch này đã được xử lý.", HttpStatusCode.Conflict);
            if (slot.StartTime <= DateTime.UtcNow)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Lịch đã quá hạn, không thể duyệt.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            if (approve)
            {
                slot.IsApproved = true;
                slot.RejectedAt = null;
                slot.RejectionReason = null;
            }
            else
            {
                slot.IsApproved = false;
                slot.RejectedAt = now;
                slot.RejectionReason = rejectionReason!.Trim();
            }
            slot.AdminName = user is not null ? $"{user.FirstName} {user.LastName}".Trim() : null;
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<MentorAvailabilityResponse>.Success(MapAvailability(slot));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not review Mentor availability {AvailabilityId}.", id);
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể xử lý duyệt lịch.", HttpStatusCode.InternalServerError);
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

    /// <summary>Slot được coi là "đã có người đặt" nếu BookedCount > 0 hoặc còn booking chưa hủy (kể cả đang giữ chỗ thanh toán).</summary>
    private async Task<bool> HasActiveBookingAsync(MentorAvailability slot, CancellationToken cancellationToken)
    {
        if (slot.BookedCount > 0) return true;
        return await unitOfWork.MentorBookings.Query()
            .AnyAsync(b => b.MentorAvailabilityId == slot.Id
                && !b.IsDeleted
                && b.Status != MentorStatus.Cancelled, cancellationToken);
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
        var roomId = Guid.NewGuid();
        return new MentorBooking
        {
            UserSubscription = subscription,
            MentorAvailability = slot,
            Status = MentorStatus.Assigned,
            ScheduledAt = slot.StartTime,
            MeetingLink = $"{appSettings.FrontendBaseUrl.TrimEnd('/')}/mentor-meeting/{roomId:D}"
        };
    }

    private static MentorMeetingPlanResponse MapPlan(SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        DurationDays = plan.DurationDays,
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