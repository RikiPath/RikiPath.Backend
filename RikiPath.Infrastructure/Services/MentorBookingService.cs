using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorBookingService : IMentorBookingService
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimService _claimService;
    private readonly IMentorCreditLedgerService _creditLedgerService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MentorBookingService> _logger;

    public MentorBookingService(
        AppDbContext context,
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        IMentorCreditLedgerService creditLedgerService,
        AppSettings appSettings,
        ILogger<MentorBookingService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _claimService = claimService;
        _creditLedgerService = creditLedgerService;
        _appSettings = appSettings;
        _logger = logger;
    }

    public Task<ApiResponse<MentorBookingResponse>> BookSlotAsync(
        BookIncludedMeetingRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAtomicBookingAsync(request, null, cancellationToken);
    }

    public async Task<ApiResponse<List<MentorBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Learner)
            return ApiResponse<List<MentorBookingResponse>>.Fail("Chỉ học viên mới được xem lịch tư vấn của mình.", HttpStatusCode.Forbidden);

        var bookings = await _unitOfWork.MentorBookings.GetByLearnerAsync(claim.Id, cancellationToken);
        return ApiResponse<List<MentorBookingResponse>>.Success(bookings.Select(x => MapBooking(x, claim.Id)).ToList());
    }

    public async Task<ApiResponse<List<MentorBookingResponse>>> GetMyMentorBookingsAsync(CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
            return ApiResponse<List<MentorBookingResponse>>.Fail("Chỉ cố vấn mới được xem lịch tư vấn của mình.", HttpStatusCode.Forbidden);

        var bookings = await _unitOfWork.MentorBookings.GetPaidBookingsForMentorAsync(claim.Id, cancellationToken);
        return ApiResponse<List<MentorBookingResponse>>.Success(bookings.Select(x => MapBooking(x, claim.Id)).ToList());
    }

    public async Task<ApiResponse<MentorBookingResponse>> GetMentorBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
            return ApiResponse<MentorBookingResponse>.Fail("Chỉ cố vấn mới được xem lịch tư vấn này.", HttpStatusCode.Forbidden);

        var booking = await _unitOfWork.MentorBookings.GetForChatAsync(bookingId, cancellationToken);
        if (booking is null || booking.MentorAvailability?.MentorId != claim.Id ||
            booking.UserSubscription?.PaymentStatus != PaymentStatus.Paid)
            return ApiResponse<MentorBookingResponse>.Fail("Không tìm thấy lịch tư vấn.", HttpStatusCode.NotFound);

        return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claim.Id));
    }

    public async Task<ApiResponse<MentorBookingResponse>> ReleaseBookingHoldAsync(
        int bookingId, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            return ApiResponse<MentorBookingResponse>.Fail("Mã lịch hẹn phải lớn hơn 0.");

        try
        {
            var claim = _claimService.GetUserClaim();
            var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

            if (isInMemory)
            {
                return await ExecuteReleaseInternalAsync(bookingId, claim, null, reason, cancellationToken);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                return await ExecuteReleaseInternalAsync(bookingId, claim, tx, reason, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi giải phóng giữ chỗ cho lịch hẹn {BookingId}: {Message}", bookingId, ex.Message);
            return ApiResponse<MentorBookingResponse>.Fail("Không thể giải phóng giữ chỗ lịch hẹn.", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<ApiResponse<MentorBookingResponse>> ExecuteReleaseInternalAsync(
        int bookingId,
        ClaimDTO claim,
        IDbContextTransaction? tx,
        string? reason,
        CancellationToken cancellationToken)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        var booking = await _context.MentorBookings
            .Include(b => b.UserSubscription)
            .Include(b => b.MentorAvailability)
                .ThenInclude(a => a!.Mentor)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

        if (booking is null)
            return ApiResponse<MentorBookingResponse>.Fail("Không tìm thấy thông tin lịch hẹn.", HttpStatusCode.NotFound);

        // Kiểm tra quyền: chỉ chính học viên đặt hoặc Quản trị viên mới được hủy/giải phóng
        if (claim.Role != Role.Admin && booking.UserSubscription.UserId != claim.Id)
            return ApiResponse<MentorBookingResponse>.Fail("Bạn không có quyền thao tác trên lịch hẹn này.", HttpStatusCode.Forbidden);

        if (booking.Status == MentorStatus.Cancelled)
            return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claim.Id), message: "Lịch hẹn đã được hủy trước đó (idempotent).");

        if (booking.Status == MentorStatus.Completed)
            return ApiResponse<MentorBookingResponse>.Fail("Buổi tư vấn đã hoàn thành, không thể hủy hoặc giải phóng giữ chỗ.", HttpStatusCode.Conflict);

        var nowUtc = DateTime.UtcNow;
        var slot = booking.MentorAvailability;

        // Chặn learner giải phóng giữ chỗ sau khi buổi học đã bắt đầu
        if (slot is not null && nowUtc >= slot.StartTime && claim.Role != Role.Admin)
        {
            return ApiResponse<MentorBookingResponse>.Fail(
                "Buổi tư vấn đã bắt đầu hoặc quá giờ hẹn, không thể giải phóng giữ chỗ.", HttpStatusCode.Conflict);
        }

        // Kiểm tra điều kiện hoàn vé: chỉ hoàn 1 Ticket khi hủy trước >= 24h hoặc Admin can thiệp
        var isRefundEligible = claim.Role == Role.Admin || slot is null || (slot.StartTime - nowUtc).TotalHours >= 24;

        // 1. Cập nhật trạng thái booking sang Cancelled và metadata
        booking.Status = MentorStatus.Cancelled;
        booking.CancellationReason = reason ?? (isRefundEligible ? "Hủy lịch hẹn và giải phóng giữ chỗ" : "Học viên hủy muộn (< 24h)");
        booking.CancelledAtUtc = nowUtc;
        booking.CancelledByUserId = claim.Id;
        booking.CancelledByRole = claim.Role.ToString();
        _context.MentorBookings.Update(booking);

        // 2. Nhả bản ghi reservation (cập nhật ReleasedAtUtc để không còn chiếm slot và không chặn GiST)
        var reservation = await _context.MentorBookingReservations
            .FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id && r.ReleasedAtUtc == null, cancellationToken);

        if (reservation is not null)
        {
            reservation.ReleasedAtUtc = nowUtc;
            _context.MentorBookingReservations.Update(reservation);
        }

        // 3. Giảm BookedCount của slot để slot có thể được đặt lại nếu vẫn hợp lệ
        if (booking.MentorAvailabilityId.HasValue)
        {
            if (isInMemory)
            {
                var s = await _context.MentorAvailabilities
                    .FirstOrDefaultAsync(x => x.Id == booking.MentorAvailabilityId.Value && x.BookedCount > 0, cancellationToken);
                if (s is not null) s.BookedCount--;
            }
            else
            {
                await _context.MentorAvailabilities
                    .Where(x => x.Id == booking.MentorAvailabilityId.Value && x.BookedCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, x => x.BookedCount - 1), cancellationToken);
            }
        }

        // 4. Giảm ActiveBookingCount của Day Guard
        if (reservation is not null)
        {
            if (isInMemory)
            {
                var g = await _context.MentorBookingDayGuards
                    .FirstOrDefaultAsync(g => g.MentorId == reservation.MentorId && g.LocalBusinessDate == reservation.LocalBusinessDate && g.ActiveBookingCount > 0, cancellationToken);
                if (g is not null) g.ActiveBookingCount--;
            }
            else
            {
                await _context.MentorBookingDayGuards
                    .Where(g => g.MentorId == reservation.MentorId && g.LocalBusinessDate == reservation.LocalBusinessDate && g.ActiveBookingCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveBookingCount, g => g.ActiveBookingCount - 1), cancellationToken);
            }
        }

        // 5. Hoàn trả tín dụng về nguồn cấp ban đầu thông qua Credit Ledger nếu đủ điều kiện (>= 24h)
        if (isRefundEligible && booking.UserSubscription != null)
        {
            var refundResult = await _creditLedgerService.RefundCreditAsync(
                booking.UserSubscription.UserId,
                booking.Id,
                $"refund:booking:{booking.Id}",
                reason ?? "Hủy lịch hẹn tư vấn và giải phóng giữ chỗ",
                cancellationToken);

            if (!refundResult.IsSuccess && !refundResult.ErrorMessage?.Contains("trước đó") == true)
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                return ApiResponse<MentorBookingResponse>.Fail(refundResult.ErrorMessage ?? "Không thể hoàn lại lượt tư vấn.", HttpStatusCode.Conflict);
            }
        }

        // 6. Ghi nhận sự kiện Outbox (BookingReleased)
        var outboxEvent = new MentorOutboxEvent
        {
            EventKey = $"booking:released:{booking.Id}:{nowUtc.Ticks}",
            AggregateType = "MentorBooking",
            AggregateId = booking.Id.ToString(),
            EventType = "BookingReleased",
            EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new
            {
                BookingId = booking.Id,
                MentorId = slot?.MentorId ?? 0,
                LearnerId = booking.UserSubscription?.UserId ?? 0,
                Reason = reason,
                TimestampUtc = nowUtc
            }),
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc
        };
        _context.MentorOutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return ApiResponse<MentorBookingResponse>.Success(MapBooking(booking, claim.Id), message: "Giải phóng giữ chỗ và hoàn lượt tư vấn thành công.");
    }

    private async Task<ApiResponse<MentorBookingResponse>> ExecuteAtomicBookingAsync(
        BookIncludedMeetingRequest request,
        int? subscriptionId,
        CancellationToken cancellationToken)
    {
        // 1. Xác thực cơ bản & Vai trò
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Learner)
            return ApiResponse<MentorBookingResponse>.Fail("Chỉ học viên mới có thể đặt lịch tư vấn 1-1.", HttpStatusCode.Forbidden);

        var learnerId = claim.Id;
        if (request.MentorAvailabilityId <= 0)
            return ApiResponse<MentorBookingResponse>.Fail("Mã khung giờ phải lớn hơn 0.");

        if (request.Question?.Length > 2000)
            return ApiResponse<MentorBookingResponse>.Fail("Câu hỏi tối đa 2000 ký tự.");

        var opKey = string.IsNullOrWhiteSpace(request.OperationKey)
            ? $"booking:learner:{learnerId}:slot:{request.MentorAvailabilityId}"
            : request.OperationKey.Trim();

        // 2. Kiểm tra Idempotency trước khi lock
        var existingEntry = await _context.MentorCreditEntries
            .Include(e => e.MentorBooking)
                .ThenInclude(b => b!.UserSubscription)
            .Include(e => e.MentorBooking)
                .ThenInclude(b => b!.MentorAvailability)
                    .ThenInclude(a => a!.Mentor)
            .FirstOrDefaultAsync(e => e.OperationKey == opKey, cancellationToken);

        if (existingEntry?.MentorBooking is not null)
        {
            var mapped = MapBooking(existingEntry.MentorBooking, learnerId);
            return ApiResponse<MentorBookingResponse>.Success(mapped, message: "Lịch hẹn đã được đặt trước đó (idempotent).");
        }

        // Tự động đảm bảo các gói học đã thanh toán hợp lệ có nguồn cấp tín dụng trong Credit Ledger
        await EnsureGrantsForPaidSubscriptionsAsync(learnerId, cancellationToken);

        // 3. Thực thi transaction nguyên tử (Atomic Transaction)
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        if (isInMemory)
        {
            return await ExecuteAtomicBookingInternalAsync(request, subscriptionId, learnerId, opKey, null, cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            return await ExecuteAtomicBookingInternalAsync(request, subscriptionId, learnerId, opKey, tx, cancellationToken);
        });
    }

    private async Task<ApiResponse<MentorBookingResponse>> ExecuteAtomicBookingInternalAsync(
        BookIncludedMeetingRequest request,
        int? subscriptionId,
        int learnerId,
        string opKey,
        IDbContextTransaction? tx,
        CancellationToken cancellationToken)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        try
        {
            var nowUtc = DateTime.UtcNow;

            // A. Kiểm tra tính hợp lệ và điều kiện của Slot
            var slot = await _context.MentorAvailabilities
                .Include(x => x.Mentor)
                    .ThenInclude(m => m!.MentorProfile)
                .FirstOrDefaultAsync(x => x.Id == request.MentorAvailabilityId && !x.IsDeleted, cancellationToken);

            if (slot is null)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ không tồn tại.", HttpStatusCode.NotFound);

            if (!slot.IsApproved || slot.RejectedAt is not null || slot.CancelRequestedAt is not null)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ chưa được phê duyệt hoặc đã bị hủy.", HttpStatusCode.Conflict);

            if (slot.BookedCount >= slot.MaxLearners)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ đã có người đặt trước.", HttpStatusCode.Conflict);

            if (slot.StartTime <= nowUtc)
                return ApiResponse<MentorBookingResponse>.Fail("Không thể đặt khung giờ trong quá khứ.", HttpStatusCode.Conflict);

            // Bắt buộc Mentor phải có hồ sơ và trạng thái xác minh Approved
            var mentorProfile = slot.Mentor?.MentorProfile;
            if (mentorProfile is null || mentorProfile.VerificationStatus != MentorVerificationStatus.Approved)
                return ApiResponse<MentorBookingResponse>.Fail("Chuyên gia chưa được phê duyệt hồ sơ chính thức.", HttpStatusCode.Conflict);

            // Lead-time tối thiểu 2 giờ
            if (slot.StartTime < nowUtc.AddHours(MentorSlotPolicy.MinimumBookingLeadHours))
                return ApiResponse<MentorBookingResponse>.Fail($"Cần đặt lịch trước tối thiểu {MentorSlotPolicy.MinimumBookingLeadHours} giờ.", HttpStatusCode.Conflict);

            // Horizon tối đa 30 ngày
            if (slot.StartTime > nowUtc.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
                return ApiResponse<MentorBookingResponse>.Fail($"Không thể đặt lịch trước quá {MentorSlotPolicy.MaximumScheduleHorizonDays} ngày.", HttpStatusCode.Conflict);

            // Thời lượng chuẩn 90 phút (bác bỏ khung giờ legacy 50/60 phút)
            var durationMinutes = (int)Math.Round((slot.EndTime - slot.StartTime).TotalMinutes);
            if (durationMinutes != MentorSlotPolicy.AppointmentDurationMinutes)
                return ApiResponse<MentorBookingResponse>.Fail($"Chỉ hỗ trợ đặt khung giờ chuẩn {MentorSlotPolicy.AppointmentDurationMinutes} phút.", HttpStatusCode.Conflict);

            // Kiểm tra active reservation trùng slot
            var hasActiveReservation = await _context.MentorBookingReservations
                .AnyAsync(r => r.MentorAvailabilityId == slot.Id && r.ReleasedAtUtc == null, cancellationToken);
            if (hasActiveReservation)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ đã có người giữ chỗ.", HttpStatusCode.Conflict);

            // Kiểm tra bảo vệ nghỉ 15 phút (15-min Rest Protection)
            var restProtectedUntilUtc = slot.EndTime.AddMinutes(MentorSlotPolicy.MinimumRestMinutes);
            var hasRestOverlap = await _context.MentorBookingReservations
                .AnyAsync(r => r.MentorId == slot.MentorId && r.ReleasedAtUtc == null
                    && slot.StartTime < r.RestProtectedUntilUtc
                    && restProtectedUntilUtc > r.StartUtc, cancellationToken);
            if (hasRestOverlap)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ xung đột với khoảng thời gian nghỉ 15 phút bắt buộc của chuyên gia.", HttpStatusCode.Conflict);

            // Kiểm tra giới hạn số buổi tối đa trong ngày (Day Guard)
            var tz = TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId);
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(slot.StartTime, tz));

            var guard = await _context.MentorBookingDayGuards
                .FirstOrDefaultAsync(g => g.MentorId == slot.MentorId && g.LocalBusinessDate == localDate, cancellationToken);
            if (guard is not null && guard.ActiveBookingCount >= MentorSlotPolicy.DefaultDailyBookingCap)
                return ApiResponse<MentorBookingResponse>.Fail($"Chuyên gia đã đạt giới hạn tối đa {MentorSlotPolicy.DefaultDailyBookingCap} buổi tư vấn trong ngày.", HttpStatusCode.Conflict);

            // Kiểm tra trùng giờ phía học viên (Learner Overlapping Booking Check)
            var hasLearnerOverlap = await _context.MentorBookings
                .Include(b => b.MentorAvailability)
                .AnyAsync(b => !b.IsDeleted && b.UserSubscription.UserId == learnerId
                    && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted)
                    && b.MentorAvailability != null
                    && slot.StartTime < b.MentorAvailability.EndTime
                    && slot.EndTime > b.MentorAvailability.StartTime, cancellationToken);
            if (hasLearnerOverlap)
                return ApiResponse<MentorBookingResponse>.Fail("Bạn đã có lịch tư vấn khác trùng với khung giờ này.", HttpStatusCode.Conflict);

            // B. Tìm nguồn cấp tín dụng đủ điều kiện theo thứ tự FIFO
            var query = _context.MentorCreditGrants
                .Include(g => g.UserSubscription)
                .Where(g => g.LearnerId == learnerId && g.AvailableQuantity > 0 && g.ExpiresAtUtc > slot.EndTime);

            if (subscriptionId.HasValue)
                query = query.Where(g => g.UserSubscriptionId == subscriptionId.Value);

            var eligibleGrant = await query
                .OrderBy(g => g.ExpiresAtUtc)
                .ThenBy(g => g.CreatedAtUtc)
                .ThenBy(g => g.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (eligibleGrant is null)
                return ApiResponse<MentorBookingResponse>.Fail("Bạn không còn đủ số lượt tư vấn hợp lệ hoặc lượt đã hết hạn.", HttpStatusCode.Conflict);

            // C. Khóa và tăng BookedCount trên slot bằng lệnh UPDATE điều kiện (Atomic Database Update)
            int slotUpdated;
            if (isInMemory)
            {
                var s = await _context.MentorAvailabilities
                    .FirstOrDefaultAsync(x => x.Id == slot.Id && x.BookedCount == 0 && !x.IsDeleted && x.IsApproved && x.RejectedAt == null, cancellationToken);
                if (s is not null)
                {
                    s.BookedCount = 1;
                    slotUpdated = 1;
                }
                else
                {
                    slotUpdated = 0;
                }
            }
            else
            {
                slotUpdated = await _context.MentorAvailabilities
                    .Where(x => x.Id == slot.Id && x.BookedCount == 0 && !x.IsDeleted && x.IsApproved && x.RejectedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, 1), cancellationToken);
            }

            if (slotUpdated == 0)
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ vừa có người khác đặt. Vui lòng chọn khung giờ khác.", HttpStatusCode.Conflict);

            // D. Trừ 1 credit khỏi nguồn cấp bằng lệnh UPDATE điều kiện (Chống double-spend ở mức database)
            int grantDeducted;
            if (isInMemory)
            {
                var g = await _context.MentorCreditGrants
                    .FirstOrDefaultAsync(g => g.Id == eligibleGrant.Id && g.AvailableQuantity > 0, cancellationToken);
                if (g is not null)
                {
                    g.AvailableQuantity--;
                    grantDeducted = 1;
                }
                else
                {
                    grantDeducted = 0;
                }
            }
            else
            {
                grantDeducted = await _context.MentorCreditGrants
                    .Where(g => g.Id == eligibleGrant.Id && g.AvailableQuantity > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.AvailableQuantity, g => g.AvailableQuantity - 1), cancellationToken);
            }

            if (grantDeducted == 0)
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                return ApiResponse<MentorBookingResponse>.Fail("Số dư lượt tư vấn không đủ hoặc đã bị trừ ở giao dịch khác.", HttpStatusCode.Conflict);
            }

            // E. Cập nhật Day Guard
            if (guard is null)
            {
                guard = new MentorBookingDayGuard
                {
                    MentorId = slot.MentorId,
                    LocalBusinessDate = localDate,
                    ActiveBookingCount = 1
                };
                _context.MentorBookingDayGuards.Add(guard);
            }
            else
            {
                guard.ActiveBookingCount++;
                _context.MentorBookingDayGuards.Update(guard);
            }

            // F. Tạo bản ghi MentorBooking
            // Lịch theo chính sách hiện tại được xác nhận ngay sau khi giữ chỗ và trừ lượt thành công.
            // Trạng thái Assigned của lịch cũ vẫn được xử lý bởi luồng tiếp nhận hiện có.
            var booking = new MentorBooking
            {
                UserSubscriptionId = eligibleGrant.UserSubscriptionId,
                MentorAvailabilityId = slot.Id,
                Status = MentorStatus.Accepted,
                ScheduledAt = slot.StartTime,
                MeetingLink = $"{_appSettings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{slot.RoomId:D}/",
                Question = request.Question?.Trim(),
                CreatedDate = nowUtc
            };
            _context.MentorBookings.Add(booking);
            await _context.SaveChangesAsync(cancellationToken);

            // G. Tạo bản ghi MentorBookingReservation (Bảo vệ GiST exclusion)
            var reservation = new MentorBookingReservation
            {
                MentorId = slot.MentorId,
                MentorBookingId = booking.Id,
                MentorAvailabilityId = slot.Id,
                LocalBusinessDate = localDate,
                StartUtc = slot.StartTime,
                EndUtc = slot.EndTime,
                RestProtectedUntilUtc = restProtectedUntilUtc,
                PolicyVersion = 3,
                CreatedAtUtc = nowUtc,
                ReleasedAtUtc = null
            };
            _context.MentorBookingReservations.Add(reservation);

            // H. Tạo bản ghi MentorBookingTerms (Điều khoản cam kết)
            var terms = new MentorBookingTerms
            {
                MentorBookingId = booking.Id,
                MentorAvailabilityId = slot.Id,
                PolicyVersion = 3,
                ScheduledStartUtc = slot.StartTime,
                ScheduledEndUtc = slot.EndTime,
                ConfirmedAtUtc = nowUtc
            };
            _context.MentorBookingTerms.Add(terms);

            // I. Tạo bút toán sổ cái MentorCreditEntry (Append-only ledger entry)
            var creditEntry = new MentorCreditEntry
            {
                CreditGrantId = eligibleGrant.Id,
                LearnerId = learnerId,
                EntryType = CreditEntryType.Spend,
                Amount = -1,
                OperationKey = opKey,
                MentorBookingId = booking.Id,
                Reason = $"Đặt lịch tư vấn #{booking.Id} cho khung giờ #{slot.Id}",
                CreatedAtUtc = nowUtc
            };
            _context.MentorCreditEntries.Add(creditEntry);

            // J. Đồng bộ số lượt đã dùng trên UserSubscription
            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == eligibleGrant.UserSubscriptionId, cancellationToken);
            if (sub is not null)
            {
                sub.MeetingSessionsUsed = Math.Min(sub.MeetingSessionsIncluded, sub.MeetingSessionsUsed + 1);
                _context.UserSubscriptions.Update(sub);
            }

            // K. Ghi nhận sự kiện Outbox (BookingCreated)
            var outboxEvent = new MentorOutboxEvent
            {
                EventKey = $"booking:created:{booking.Id}:{nowUtc.Ticks}",
                AggregateType = "MentorBooking",
                AggregateId = booking.Id.ToString(),
                EventType = "BookingCreated",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    BookingId = booking.Id,
                    MentorId = slot.MentorId,
                    LearnerId = learnerId,
                    SlotId = slot.Id,
                    ScheduledAtUtc = slot.StartTime,
                    EndTimeUtc = slot.EndTime,
                    MeetingLink = booking.MeetingLink,
                    Question = booking.Question,
                    TimestampUtc = nowUtc
                }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            };
            _context.MentorOutboxEvents.Add(outboxEvent);

            await _context.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);

            var response = MapBooking(booking, learnerId);
            return ApiResponse<MentorBookingResponse>.Success(response, HttpStatusCode.Created, "Đặt lịch tư vấn thành công.");
        }
        catch (DbUpdateException ex)
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "DbUpdateException trong giao dịch đặt lịch: {Message}", ex.Message);

            var exStr = ex.ToString();
            if (exStr.Contains("23P01") || exStr.Contains("EX_MentorBookingReservation_ActiveMentorRest") || exStr.Contains("exclusion"))
                return ApiResponse<MentorBookingResponse>.Fail("Khung giờ vi phạm thời gian nghỉ 15 phút của chuyên gia hoặc bị trùng lịch.", HttpStatusCode.Conflict);

            if (exStr.Contains("23505") || exStr.Contains("IX_MentorCreditEntries_OperationKey"))
            {
                var reloaded = await _context.MentorCreditEntries
                    .Include(e => e.MentorBooking)
                        .ThenInclude(b => b!.UserSubscription)
                    .Include(e => e.MentorBooking)
                        .ThenInclude(b => b!.MentorAvailability)
                            .ThenInclude(a => a!.Mentor)
                    .FirstOrDefaultAsync(e => e.OperationKey == opKey, cancellationToken);

                if (reloaded?.MentorBooking != null)
                    return ApiResponse<MentorBookingResponse>.Success(MapBooking(reloaded.MentorBooking, learnerId), message: "Lịch hẹn đã được đặt trước đó (idempotent).");
            }

            return ApiResponse<MentorBookingResponse>.Fail("Khung giờ vừa có người khác đặt hoặc xảy ra xung đột dữ liệu.", HttpStatusCode.Conflict);
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi không xác định khi thực hiện đặt lịch nguyên tử.");
            return ApiResponse<MentorBookingResponse>.Fail("Không thể đặt lịch tư vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
        }
    }

    private async Task EnsureGrantsForPaidSubscriptionsAsync(int learnerId, CancellationToken cancellationToken)
    {
        try
        {
            var paidSubsWithoutGrant = await _context.UserSubscriptions
                .Where(s => s.UserId == learnerId && !s.IsDeleted && s.PaymentStatus == PaymentStatus.Paid && s.MeetingSessionsIncluded > s.MeetingSessionsUsed)
                .Where(s => !_context.MentorCreditGrants.Any(g => g.UserSubscriptionId == s.Id))
                .ToListAsync(cancellationToken);

            foreach (var sub in paidSubsWithoutGrant)
            {
                await _creditLedgerService.CreateGrantFromPaidSubscriptionAsync(sub.Id, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tự động đồng bộ grant từ gói học: {Message}", ex.Message);
        }
    }

    private static MentorBookingResponse MapBooking(MentorBooking booking, int learnerId)
    {
        return new MentorBookingResponse
        {
            Id = booking.Id,
            UserSubscriptionId = booking.UserSubscriptionId,
            MentorAvailabilityId = booking.MentorAvailabilityId,
            MentorId = booking.MentorAvailability?.MentorId,
            MentorName = booking.MentorAvailability?.Mentor != null
                ? $"{booking.MentorAvailability.Mentor.FirstName} {booking.MentorAvailability.Mentor.LastName}".Trim()
                : null,
            LearnerId = booking.UserSubscription?.UserId ?? learnerId,
            LearnerName = booking.UserSubscription?.UserAccount != null
                ? $"{booking.UserSubscription.UserAccount.FirstName} {booking.UserSubscription.UserAccount.LastName}".Trim()
                : null,
            Status = booking.Status,
            PaymentStatus = booking.UserSubscription?.PaymentStatus,
            ScheduledAt = booking.ScheduledAt,
            MeetingLink = booking.MeetingLink,
            RoomId = booking.MentorAvailability?.RoomId,
            Question = booking.Question,
            EndsAt = booking.MentorAvailability?.EndTime,
            CreatedAt = booking.CreatedDate
        };
    }
}
