using System.Net;
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
            if (slot is null)
                return ApiResponse<MentorMeetingPurchaseResponse>.Fail("Slot không còn trống hoặc không hợp lệ.", HttpStatusCode.Conflict);

            var expiresAt = now.AddMinutes(15);
            var subscription = new UserSubscription
            {
                UserId = claims.Id,
                SubscriptionPlanId = plan.Id,
                SubscriptionPlan = plan,
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
                ScheduledAt = slot.StartTime
            };

            slot.IsBooked = true;
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
                    payOs.WebReturnUrl,
                    payOs.WebCancelUrl,
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
                slot.IsBooked = false;
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
            if (slot is null)
                return ApiResponse<MentorBookingResponse>.Fail("Slot không còn trống hoặc không hợp lệ.", HttpStatusCode.Conflict);

            slot.IsBooked = true;
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
                Bookings = subscription.MentorBookings.Select(x => MapBooking(x, learnerId)).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load Mentor Meeting purchase status.");
            return ApiResponse<MentorMeetingPurchaseStatusResponse>.Fail("Không thể tải trạng thái giao dịch.", HttpStatusCode.InternalServerError);
        }
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
            if (request.StartTime <= now || request.EndTime <= request.StartTime)
                return ApiResponse<MentorAvailabilityResponse>.Fail("Thời gian bắt đầu phải ở tương lai và trước thời gian kết thúc.");
            if (request.EndTime - request.StartTime > TimeSpan.FromHours(4))
                return ApiResponse<MentorAvailabilityResponse>.Fail("Một slot không được dài quá 4 giờ.");
            if (await unitOfWork.MentorAvailabilities.HasOverlappingSlotAsync(claims.Id, request.StartTime, request.EndTime, cancellationToken))
                return ApiResponse<MentorAvailabilityResponse>.Fail("Slot bị trùng với lịch đã tạo.", HttpStatusCode.Conflict);

            var slot = new MentorAvailability
            {
                MentorId = claims.Id,
                StartTime = request.StartTime,
                EndTime = request.EndTime
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

    public async Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return ApiResponse<bool>.Fail("AvailabilityId phải lớn hơn 0.");
        try
        {
            var claims = claimService.GetUserClaim();
            if (claims.Role != Role.Mentor)
                return ApiResponse<bool>.Fail("Chỉ Mentor mới có thể xóa lịch của mình.", HttpStatusCode.Forbidden);
            var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
            if (slot is null || slot.IsDeleted || slot.MentorId != claims.Id)
                return ApiResponse<bool>.Fail("Không tìm thấy khung giờ.", HttpStatusCode.NotFound);
            if (slot.IsBooked)
                return ApiResponse<bool>.Fail("Không thể xóa slot đang được giữ hoặc đã đặt.", HttpStatusCode.Conflict);
            slot.IsDeleted = true;
            slot.ModifiedDate = DateTime.UtcNow;
            unitOfWork.MentorAvailabilities.Update(slot);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Success(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not delete Mentor availability {AvailabilityId}.", id);
            return ApiResponse<bool>.Fail("Không thể xóa khung giờ.", HttpStatusCode.InternalServerError);
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
                slot.IsBooked = false;
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
            RoomId = roomId,
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
        IsBooked = slot.IsBooked
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
            RoomId = booking.RoomId
        };
    }
}
