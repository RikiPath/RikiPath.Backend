using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorCancellationService : IMentorCancellationService
{
    private readonly AppDbContext _context;
    private readonly IClaimService _claimService;
    private readonly IMentorCreditLedgerService _creditLedgerService;
    private readonly IMentorAttendanceService _attendanceService;
    private readonly ILogger<MentorCancellationService> _logger;

    public MentorCancellationService(
        AppDbContext context,
        IClaimService claimService,
        IMentorCreditLedgerService creditLedgerService,
        IMentorAttendanceService attendanceService,
        ILogger<MentorCancellationService> logger)
    {
        _context = context;
        _claimService = claimService;
        _creditLedgerService = creditLedgerService;
        _attendanceService = attendanceService;
        _logger = logger;
    }

    public async Task<ApiResponse<MentorCancellationResponse>> CancelBookingAsync(
        int bookingId,
        CancelMentorBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            return ApiResponse<MentorCancellationResponse>.Fail("Mã lịch hẹn phải lớn hơn 0.");

        try
        {
            var claim = _claimService.GetUserClaim();
            var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

            if (isInMemory)
            {
                return await ExecuteCancelInternalAsync(bookingId, request, claim, null, cancellationToken);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                return await ExecuteCancelInternalAsync(bookingId, request, claim, tx, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý hủy lịch hẹn {BookingId}: {Message}", bookingId, ex.Message);
            return ApiResponse<MentorCancellationResponse>.Fail("Không thể thực hiện hủy lịch hẹn tư vấn.", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<MentorCancellationResponse>> ExecuteCancelInternalAsync(
        int bookingId,
        CancelMentorBookingRequest request,
        ClaimDTO claim,
        IDbContextTransaction? tx,
        CancellationToken cancellationToken)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        var booking = await _context.MentorBookings
            .Include(b => b.UserSubscription)
            .Include(b => b.MentorAvailability)
                .ThenInclude(a => a!.Mentor)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

        if (booking is null)
            return ApiResponse<MentorCancellationResponse>.Fail("Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);

        var isOwnerLearner = booking.UserSubscription != null && booking.UserSubscription.UserId == claim.Id;
        var isAssignedMentor = booking.MentorAvailability != null && booking.MentorAvailability.MentorId == claim.Id;
        var isAdmin = claim.Role == Role.Admin;

        if (!isOwnerLearner && !isAssignedMentor && !isAdmin)
            return ApiResponse<MentorCancellationResponse>.Fail("Bạn không có quyền hủy lịch hẹn tư vấn này.", HttpStatusCode.Forbidden);

        if (booking.Status == MentorStatus.Cancelled)
        {
            return ApiResponse<MentorCancellationResponse>.Success(new MentorCancellationResponse
            {
                BookingId = booking.Id,
                Status = booking.Status,
                CancellationReason = booking.CancellationReason,
                CancelledBy = booking.CancelledByRole ?? "Unknown",
                CancelledAtUtc = booking.CancelledAtUtc ?? DateTime.UtcNow,
                IsRefunded = false,
                ReleasedSlotId = booking.MentorAvailabilityId,
                Message = "Lịch hẹn đã được hủy trước đó (idempotent)."
            });
        }

        if (booking.Status == MentorStatus.Completed)
            return ApiResponse<MentorCancellationResponse>.Fail("Buổi tư vấn đã hoàn thành, không thể hủy.", HttpStatusCode.Conflict);

        var nowUtc = DateTime.UtcNow;
        var slot = booking.MentorAvailability;

        if (slot is not null)
        {
            var isRoomActiveWindow = nowUtc >= slot.StartTime.AddMinutes(-15) && nowUtc <= slot.EndTime.AddMinutes(30);
            var isUserOnline = _attendanceService.IsUserOnlineInRoom(slot.RoomId, claim.Id);
            if (isRoomActiveWindow && isUserOnline)
            {
                return ApiResponse<MentorCancellationResponse>.Fail(
                    "Buổi tư vấn đang diễn ra và bạn đang có mặt trong phòng họp. Vui lòng rời phòng họp trước khi yêu cầu hủy.",
                    HttpStatusCode.Conflict);
            }
        }

        var isLateCancellation = false;
        var isRefundEligible = false;
        string? refundOpKey = null;

        if (isOwnerLearner)
        {
            if (slot is null)
            {
                isRefundEligible = true;
            }
            else
            {
                var hoursUntilStart = (slot.StartTime - nowUtc).TotalHours;
                if (hoursUntilStart >= 24)
                {
                    isRefundEligible = true;
                }
                else
                {
                    isLateCancellation = true;
                    isRefundEligible = false;
                }
            }
        }
        else if (isAssignedMentor)
        {
            if (slot is not null && nowUtc >= slot.StartTime)
            {
                return ApiResponse<MentorCancellationResponse>.Fail(
                    "Không thể hủy buổi tư vấn sau khi đã đến giờ bắt đầu.", HttpStatusCode.Conflict);
            }

            isRefundEligible = true;
            if (booking.UserSubscription != null && booking.UserSubscription.EndDate < nowUtc.AddDays(7))
            {
                booking.UserSubscription.EndDate = nowUtc.AddDays(7);
                _context.UserSubscriptions.Update(booking.UserSubscription);
            }
        }
        else if (isAdmin)
        {
            isRefundEligible = true;
            if (booking.UserSubscription != null && booking.UserSubscription.EndDate < nowUtc.AddDays(7))
            {
                booking.UserSubscription.EndDate = nowUtc.AddDays(7);
                _context.UserSubscriptions.Update(booking.UserSubscription);
            }
        }

        booking.Status = MentorStatus.Cancelled;
        booking.CancellationReason = request.Reason ?? (isLateCancellation ? "Học viên hủy muộn (< 24h)" : "Hủy lịch hẹn tư vấn");
        booking.CancelledAtUtc = nowUtc;
        booking.CancelledByUserId = claim.Id;
        booking.CancelledByRole = claim.Role.ToString();
        booking.MeetingLink = null;

        if (isLateCancellation && request.RequestException)
        {
            booking.ExceptionRequested = true;
            booking.ExceptionReason = request.ExceptionReason ?? request.Reason ?? "Yêu cầu xem xét ngoại lệ hoàn vé";
        }

        _context.MentorBookings.Update(booking);

        var reservation = await _context.MentorBookingReservations
            .FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id && r.ReleasedAtUtc == null, cancellationToken);

        if (reservation is not null)
        {
            reservation.ReleasedAtUtc = nowUtc;
            _context.MentorBookingReservations.Update(reservation);
        }

        if (slot is not null)
        {
            if (isInMemory)
            {
                if (slot.BookedCount > 0) slot.BookedCount--;
                if (slot.MaxLearners == 1) slot.RoomId = Guid.NewGuid();
                if (isAssignedMentor)
                {
                    slot.CancelRequestedAt = nowUtc;
                }
            }
            else
            {
                await _context.MentorAvailabilities
                    .Where(x => x.Id == slot.Id && x.BookedCount > 0)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.BookedCount, x => x.BookedCount - 1)
                        .SetProperty(x => x.RoomId, x => slot.MaxLearners == 1 ? Guid.NewGuid() : x.RoomId)
                        .SetProperty(x => x.CancelRequestedAt, x => isAssignedMentor ? nowUtc : x.CancelRequestedAt),
                        cancellationToken);
            }
        }

        if (reservation is not null)
        {
            if (isInMemory)
            {
                var g = await _context.MentorBookingDayGuards
                    .FirstOrDefaultAsync(x => x.MentorId == reservation.MentorId && x.LocalBusinessDate == reservation.LocalBusinessDate && x.ActiveBookingCount > 0, cancellationToken);
                if (g is not null) g.ActiveBookingCount--;
            }
            else
            {
                await _context.MentorBookingDayGuards
                    .Where(x => x.MentorId == reservation.MentorId && x.LocalBusinessDate == reservation.LocalBusinessDate && x.ActiveBookingCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveBookingCount, x => x.ActiveBookingCount - 1), cancellationToken);
            }
        }

        if (isRefundEligible && booking.UserSubscription != null)
        {
            refundOpKey = $"refund:booking:{booking.Id}";
            var refundRes = await _creditLedgerService.RefundCreditAsync(
                booking.UserSubscription.UserId,
                booking.Id,
                refundOpKey,
                $"Hoàn tín dụng do hủy lịch #{booking.Id} bởi {claim.Role}",
                cancellationToken);

            if (!refundRes.IsSuccess && !refundRes.ErrorMessage?.Contains("trước đó") == true)
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                return ApiResponse<MentorCancellationResponse>.Fail(
                    refundRes.ErrorMessage ?? "Không thể hoàn trả lượt tín dụng.", HttpStatusCode.Conflict);
            }
        }

        var outboxPayload = JsonSerializer.Serialize(new
        {
            BookingId = booking.Id,
            LearnerId = booking.UserSubscription?.UserId,
            MentorId = slot?.MentorId,
            CancelledBy = claim.Role.ToString(),
            CancelledByUserId = claim.Id,
            Reason = booking.CancellationReason,
            IsLate = isLateCancellation,
            IsRefunded = isRefundEligible,
            TimestampUtc = nowUtc
        });

        var outboxEvent = new MentorOutboxEvent
        {
            EventKey = $"booking:cancelled:{booking.Id}:{nowUtc.Ticks}",
            AggregateType = "MentorBooking",
            AggregateId = booking.Id.ToString(),
            EventType = isLateCancellation ? "BookingLateCancelled" : (isAssignedMentor ? "BookingCancelledByMentor" : "BookingCancelled"),
            EventVersion = 1,
            PayloadJson = outboxPayload,
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc
        };
        _context.MentorOutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        var response = new MentorCancellationResponse
        {
            BookingId = booking.Id,
            Status = booking.Status,
            CancellationReason = booking.CancellationReason,
            CancelledBy = claim.Role.ToString(),
            CancelledAtUtc = nowUtc,
            IsRefunded = isRefundEligible,
            RefundOperationKey = refundOpKey,
            IsLateCancellation = isLateCancellation,
            ExceptionRequested = booking.ExceptionRequested,
            ExceptionApproved = booking.ExceptionApproved,
            ReleasedSlotId = booking.MentorAvailabilityId,
            Message = isLateCancellation
                ? "Lịch hẹn đã được hủy (hủy muộn sát giờ hẹn dưới 24h nên không hoàn trả lượt tín dụng)."
                : "Hủy lịch hẹn thành công. Lượt tín dụng và khung giờ đã được hoàn trả."
        };

        return ApiResponse<MentorCancellationResponse>.Success(response, HttpStatusCode.OK, response.Message);
    }

    public async Task<ApiResponse<MentorRescheduleResponse>> RescheduleBookingAsync(
        int bookingId,
        RescheduleMentorBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0 || request.NewSlotId <= 0)
            return ApiResponse<MentorRescheduleResponse>.Fail("Mã lịch hẹn và mã slot mới không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();
            var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

            if (isInMemory)
            {
                return await ExecuteRescheduleInternalAsync(bookingId, request, claim, null, cancellationToken);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                return await ExecuteRescheduleInternalAsync(bookingId, request, claim, tx, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý dời lịch hẹn {BookingId} sang slot {NewSlotId}: {Message}", bookingId, request.NewSlotId, ex.Message);
            return ApiResponse<MentorRescheduleResponse>.Fail("Không thể thực hiện dời lịch hẹn.", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<MentorRescheduleResponse>> ExecuteRescheduleInternalAsync(
        int bookingId,
        RescheduleMentorBookingRequest request,
        ClaimDTO claim,
        IDbContextTransaction? tx,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var booking = await _context.MentorBookings
            .Include(b => b.UserSubscription)
            .Include(b => b.MentorAvailability)
                .ThenInclude(a => a!.Mentor)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

        if (booking is null)
            return ApiResponse<MentorRescheduleResponse>.Fail("Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);

        if (claim.Role != Role.Admin && booking.UserSubscription.UserId != claim.Id)
            return ApiResponse<MentorRescheduleResponse>.Fail("Bạn không có quyền dời lịch hẹn này.", HttpStatusCode.Forbidden);

        if (booking.Status == MentorStatus.Cancelled)
            return ApiResponse<MentorRescheduleResponse>.Fail("Lịch hẹn đã bị hủy, không thể dời lịch.", HttpStatusCode.Conflict);

        if (booking.Status == MentorStatus.Completed)
            return ApiResponse<MentorRescheduleResponse>.Fail("Lịch hẹn đã hoàn thành, không thể dời lịch.", HttpStatusCode.Conflict);

        var oldSlot = booking.MentorAvailability;
        if (oldSlot is null)
            return ApiResponse<MentorRescheduleResponse>.Fail("Lịch hẹn không có thông tin khung giờ hợp lệ.", HttpStatusCode.BadRequest);

        if (claim.Role != Role.Admin && (oldSlot.StartTime - nowUtc).TotalHours < 24)
        {
            return ApiResponse<MentorRescheduleResponse>.Fail(
                "Chỉ được dời lịch trước giờ bắt đầu ít nhất 24 tiếng.", HttpStatusCode.Conflict);
        }

        var newSlot = await _context.MentorAvailabilities
            .Include(a => a.Mentor)
            .FirstOrDefaultAsync(a => a.Id == request.NewSlotId && !a.IsDeleted, cancellationToken);

        if (newSlot is null)
            return ApiResponse<MentorRescheduleResponse>.Fail("Không tìm thấy khung giờ mới muốn dời tới.", HttpStatusCode.NotFound);

        if (newSlot.MentorId != oldSlot.MentorId)
            return ApiResponse<MentorRescheduleResponse>.Fail("Khung giờ mới phải thuộc về cùng một cố vấn.", HttpStatusCode.BadRequest);

        if (!newSlot.IsApproved || newSlot.RejectedAt != null || newSlot.CancelRequestedAt != null)
            return ApiResponse<MentorRescheduleResponse>.Fail("Khung giờ mới chưa được kích hoạt hoặc đã bị thu hồi.", HttpStatusCode.Conflict);

        if (newSlot.StartTime <= nowUtc.AddHours(2))
            return ApiResponse<MentorRescheduleResponse>.Fail("Khung giờ mới phải cách hiện tại ít nhất 2 tiếng.", HttpStatusCode.Conflict);

        if (newSlot.BookedCount >= newSlot.MaxLearners)
            return ApiResponse<MentorRescheduleResponse>.Fail("Khung giờ mới đã được học viên khác đặt kín.", HttpStatusCode.Conflict);

        // Nếu lịch hẹn có gắn trình độ JLPT, xác minh cố vấn vẫn còn quyền giảng dạy trình độ đó
        if (booking.CompensationLevelId.HasValue)
        {
            var hasAnyQualifications = await _context.MentorTeachingQualifications
                .AnyAsync(q => q.MentorUserId == newSlot.MentorId, cancellationToken);
            if (hasAnyQualifications)
            {
                var stillQualified = await _context.MentorTeachingQualifications
                    .AnyAsync(q => q.MentorUserId == newSlot.MentorId
                        && q.CertificateLevelId == booking.CompensationLevelId.Value
                        && q.IsActive
                        && q.RevokedAtUtc == null, cancellationToken);
                if (!stillQualified)
                {
                    return ApiResponse<MentorRescheduleResponse>.Fail(
                        "Chuyên gia không còn quyền giảng dạy trình độ JLPT của lịch hẹn này, không thể dời lịch.",
                        HttpStatusCode.Conflict);
                }
            }
        }

        var oldReservation = await _context.MentorBookingReservations
            .FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id, cancellationToken);

        var localStartTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var utcSlotStart = DateTime.SpecifyKind(newSlot.StartTime, DateTimeKind.Utc);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(utcSlotStart, localStartTz);
        var localDate = DateOnly.FromDateTime(localStart);

        if (oldReservation is not null)
        {
            oldReservation.MentorAvailabilityId = newSlot.Id;
            oldReservation.MentorId = newSlot.MentorId;
            oldReservation.StartUtc = newSlot.StartTime;
            oldReservation.EndUtc = newSlot.EndTime;
            oldReservation.LocalBusinessDate = localDate;
            oldReservation.RestProtectedUntilUtc = newSlot.EndTime.AddMinutes(15);
            oldReservation.ReleasedAtUtc = null;
            oldReservation.PolicyVersion = newSlot.PolicyVersion ?? 1;
            _context.MentorBookingReservations.Update(oldReservation);
        }
        else
        {
            var newReservation = new MentorBookingReservation
            {
                MentorBookingId = booking.Id,
                MentorAvailabilityId = newSlot.Id,
                MentorId = newSlot.MentorId,
                StartUtc = newSlot.StartTime,
                EndUtc = newSlot.EndTime,
                LocalBusinessDate = localDate,
                RestProtectedUntilUtc = newSlot.EndTime.AddMinutes(15),
                CreatedAtUtc = nowUtc,
                PolicyVersion = newSlot.PolicyVersion ?? 1
            };
            _context.MentorBookingReservations.Add(newReservation);
        }

        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        if (isInMemory)
        {
            if (oldSlot.BookedCount > 0) oldSlot.BookedCount--;
        }
        else
        {
            await _context.MentorAvailabilities
                .Where(x => x.Id == oldSlot.Id && x.BookedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, x => x.BookedCount - 1), cancellationToken);
        }

        if (isInMemory)
        {
            newSlot.BookedCount++;
        }
        else
        {
            await _context.MentorAvailabilities
                .Where(x => x.Id == newSlot.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, x => x.BookedCount + 1), cancellationToken);
        }

        var prevStart = oldSlot.StartTime;
        booking.MentorAvailabilityId = newSlot.Id;
        booking.ScheduledAt = newSlot.StartTime;
        booking.Status = MentorStatus.Assigned;
        _context.MentorBookings.Update(booking);

        var outboxEvent = new MentorOutboxEvent
        {
            EventKey = $"booking:rescheduled:{booking.Id}:{nowUtc.Ticks}",
            AggregateType = "MentorBooking",
            AggregateId = booking.Id.ToString(),
            EventType = "BookingRescheduled",
            EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                LearnerId = booking.UserSubscription.UserId,
                MentorId = newSlot.MentorId,
                PreviousSlotId = oldSlot.Id,
                NewSlotId = newSlot.Id,
                PreviousStartUtc = prevStart,
                NewStartUtc = newSlot.StartTime,
                TimestampUtc = nowUtc
            }),
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc
        };
        _context.MentorOutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        var response = new MentorRescheduleResponse
        {
            BookingId = booking.Id,
            PreviousSlotId = oldSlot.Id,
            NewSlotId = newSlot.Id,
            PreviousScheduledAtUtc = prevStart,
            NewScheduledAtUtc = newSlot.StartTime,
            Status = booking.Status,
            Message = "Dời lịch hẹn thành công sang khung giờ mới."
        };

        return ApiResponse<MentorRescheduleResponse>.Success(response, HttpStatusCode.OK, response.Message);
    }

    public async Task<ApiResponse<CancellationPolicyEligibilityResponse>> CheckCancellationEligibilityAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            return ApiResponse<CancellationPolicyEligibilityResponse>.Fail("Mã lịch hẹn phải lớn hơn 0.");

        try
        {
            var claim = _claimService.GetUserClaim();
            var booking = await _context.MentorBookings
                .Include(b => b.UserSubscription)
                .Include(b => b.MentorAvailability)
                .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

            if (booking is null)
                return ApiResponse<CancellationPolicyEligibilityResponse>.Fail("Không tìm thấy thông tin lịch hẹn.", HttpStatusCode.NotFound);

            var isOwnerLearner = booking.UserSubscription != null && booking.UserSubscription.UserId == claim.Id;
            var isAssignedMentor = booking.MentorAvailability != null && booking.MentorAvailability.MentorId == claim.Id;
            var isAdmin = claim.Role == Role.Admin;

            if (!isOwnerLearner && !isAssignedMentor && !isAdmin)
                return ApiResponse<CancellationPolicyEligibilityResponse>.Fail("Bạn không có quyền truy cập thông tin lịch hẹn này.", HttpStatusCode.Forbidden);

            var nowUtc = DateTime.UtcNow;
            var slot = booking.MentorAvailability;

            if (booking.Status == MentorStatus.Cancelled)
            {
                return ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse
                {
                    BookingId = booking.Id,
                    CanCancel = false,
                    CanReschedule = false,
                    EligibleForFullRefund = false,
                    HoursUntilStart = 0,
                    Tier = "Already_Cancelled",
                    Message = "Lịch hẹn này đã bị hủy trước đó."
                });
            }

            if (booking.Status == MentorStatus.Completed)
            {
                return ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse
                {
                    BookingId = booking.Id,
                    CanCancel = false,
                    CanReschedule = false,
                    EligibleForFullRefund = false,
                    HoursUntilStart = 0,
                    Tier = "Session_Completed",
                    Message = "Buổi tư vấn đã hoàn thành."
                });
            }

            if (slot is null)
            {
                return ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse
                {
                    BookingId = booking.Id,
                    CanCancel = true,
                    CanReschedule = false,
                    EligibleForFullRefund = true,
                    HoursUntilStart = 0,
                    Tier = "Early_FullRefund",
                    Message = "Có thể hủy và hoàn trả lượt tư vấn."
                });
            }

            var hoursUntilStart = (slot.StartTime - nowUtc).TotalHours;

            if (nowUtc >= slot.StartTime)
            {
                return ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse
                {
                    BookingId = booking.Id,
                    CanCancel = isAdmin,
                    CanReschedule = false,
                    EligibleForFullRefund = isAdmin,
                    HoursUntilStart = hoursUntilStart,
                    Tier = "Session_Started",
                    Message = "Buổi tư vấn đã bắt đầu hoặc quá giờ hẹn."
                });
            }

            var isEligibleForFullRefund = isAssignedMentor || isAdmin || hoursUntilStart >= 24;
            var canReschedule = isOwnerLearner && hoursUntilStart >= 24;

            return ApiResponse<CancellationPolicyEligibilityResponse>.Success(new CancellationPolicyEligibilityResponse
            {
                BookingId = booking.Id,
                CanCancel = true,
                CanReschedule = canReschedule,
                EligibleForFullRefund = isEligibleForFullRefund,
                HoursUntilStart = Math.Round(hoursUntilStart, 2),
                Tier = hoursUntilStart >= 24 ? "Early_FullRefund" : "Late_NoRefund",
                Message = hoursUntilStart >= 24
                    ? "Đủ điều kiện hủy được hoàn 100% lượt tín dụng (từ 24 giờ trở lên)."
                    : "Hủy muộn (dưới 24 giờ): không được tự động hoàn lượt tín dụng trừ khi có lý do ngoại lệ được duyệt."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra điều kiện hủy cho booking {BookingId}: {Message}", bookingId, ex.Message);
            return ApiResponse<CancellationPolicyEligibilityResponse>.Fail("Không thể kiểm tra điều kiện hủy lịch.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorCancellationResponse>> ReviewExceptionRequestAsync(
        int bookingId,
        ReviewCancellationExceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            return ApiResponse<MentorCancellationResponse>.Fail("Mã lịch hẹn phải lớn hơn 0.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<MentorCancellationResponse>.Fail("Chỉ quản trị viên mới có quyền duyệt ngoại lệ hoàn vé.", HttpStatusCode.Forbidden);

            var booking = await _context.MentorBookings
                .Include(b => b.UserSubscription)
                .Include(b => b.MentorAvailability)
                .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

            if (booking is null)
                return ApiResponse<MentorCancellationResponse>.Fail("Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);

            if (!booking.ExceptionRequested)
                return ApiResponse<MentorCancellationResponse>.Fail("Lịch hẹn này không có yêu cầu ngoại lệ hoàn vé nào đang chờ duyệt.", HttpStatusCode.BadRequest);

            var nowUtc = DateTime.UtcNow;
            string? refundOpKey = null;

            booking.ExceptionApproved = request.ApproveRefund;
            booking.CancellationReason = $"{booking.CancellationReason} | Quyết định Admin: {(request.ApproveRefund ? "Duyệt hoàn vé" : "Từ chối")} ({request.AdminNotes})";

            if (request.ApproveRefund && booking.UserSubscription != null)
            {
                refundOpKey = $"refund:exception:{booking.Id}";
                var refundRes = await _creditLedgerService.RefundCreditAsync(
                    booking.UserSubscription.UserId,
                    booking.Id,
                    refundOpKey,
                    $"Admin phê duyệt hoàn vé ngoại lệ cho lịch hẹn #{booking.Id}",
                    cancellationToken);

                if (!refundRes.IsSuccess && !refundRes.ErrorMessage?.Contains("trước đó") == true)
                {
                    return ApiResponse<MentorCancellationResponse>.Fail(
                        refundRes.ErrorMessage ?? "Không thể hoàn trả lượt tín dụng.", HttpStatusCode.Conflict);
                }

                var extensionDays = request.ExtendDays > 0 ? request.ExtendDays : 7;
                if (booking.UserSubscription.EndDate < nowUtc.AddDays(extensionDays))
                {
                    booking.UserSubscription.EndDate = nowUtc.AddDays(extensionDays);
                    _context.UserSubscriptions.Update(booking.UserSubscription);
                }
            }

            _context.MentorBookings.Update(booking);
            await _context.SaveChangesAsync(cancellationToken);

            var response = new MentorCancellationResponse
            {
                BookingId = booking.Id,
                Status = booking.Status,
                CancellationReason = booking.CancellationReason,
                CancelledBy = booking.CancelledByRole ?? "Learner",
                CancelledAtUtc = booking.CancelledAtUtc ?? nowUtc,
                IsRefunded = request.ApproveRefund,
                RefundOperationKey = refundOpKey,
                IsLateCancellation = true,
                ExceptionRequested = true,
                ExceptionApproved = request.ApproveRefund,
                ReleasedSlotId = booking.MentorAvailabilityId,
                Message = request.ApproveRefund
                    ? "Đã phê duyệt yêu cầu ngoại lệ và hoàn trả lượt tư vấn cho học viên."
                    : "Đã từ chối yêu cầu ngoại lệ hoàn vé."
            };

            return ApiResponse<MentorCancellationResponse>.Success(response, HttpStatusCode.OK, response.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi duyệt ngoại lệ hoàn vé cho booking {BookingId}: {Message}", bookingId, ex.Message);
            return ApiResponse<MentorCancellationResponse>.Fail("Không thể duyệt yêu cầu ngoại lệ.", HttpStatusCode.InternalServerError);
        }
    }
}
