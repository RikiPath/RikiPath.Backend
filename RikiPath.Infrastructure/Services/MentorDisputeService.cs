using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorDisputeService : IMentorDisputeService
{
    private readonly AppDbContext _context;
    private readonly IClaimService _claimService;
    private readonly IMentorCreditLedgerService _creditLedgerService;
    private readonly ILogger<MentorDisputeService> _logger;

    public MentorDisputeService(
        AppDbContext context,
        IClaimService claimService,
        IMentorCreditLedgerService creditLedgerService,
        ILogger<MentorDisputeService> logger)
    {
        _context = context;
        _claimService = claimService;
        _creditLedgerService = creditLedgerService;
        _logger = logger;
    }

    public async Task<ApiResponse<MentorDisputeResponse>> CreateDisputeAsync(
        CreateMentorDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MentorBookingId <= 0)
            return ApiResponse<MentorDisputeResponse>.Fail("Mã lịch hẹn không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<MentorDisputeResponse>.Fail("Lý do khiếu nại không được để trống.");

        try
        {
            var claim = _claimService.GetUserClaim();

            var booking = await _context.MentorBookings
                .Include(b => b.UserSubscription)
                .Include(b => b.MentorAvailability)
                    .ThenInclude(a => a!.Mentor)
                .FirstOrDefaultAsync(b => b.Id == request.MentorBookingId && !b.IsDeleted, cancellationToken);

            if (booking is null)
                return ApiResponse<MentorDisputeResponse>.Fail("Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);

            var isOwner = booking.UserSubscription != null && booking.UserSubscription.UserId == claim.Id;
            var isAdmin = claim.Role == Role.Admin;
            if (!isOwner && !isAdmin)
                return ApiResponse<MentorDisputeResponse>.Fail("Bạn không có quyền gửi khiếu nại cho lịch hẹn này.", HttpStatusCode.Forbidden);

            var nowUtc = DateTime.UtcNow;
            var slot = booking.MentorAvailability;

            // Kiểm tra thời hạn gửi khiếu nại (trong vòng 48 giờ sau khi lịch hẹn kết thúc hoặc bị hủy)
            var referenceTime = booking.CancelledAtUtc ?? slot?.EndTime ?? booking.ScheduledAt ?? nowUtc;
            if (nowUtc > referenceTime.AddHours(48))
                return ApiResponse<MentorDisputeResponse>.Fail("Đã quá thời hạn 48 giờ để gửi khiếu nại cho lịch hẹn này.", HttpStatusCode.Conflict);

            // Kiểm tra chống trùng lặp khiếu nại đang mở
            var hasActiveDispute = await _context.MentorDisputes
                .AnyAsync(d => d.MentorBookingId == request.MentorBookingId &&
                               d.Status != MentorDisputeStatus.Closed &&
                               d.Status != MentorDisputeStatus.Rejected, cancellationToken);
            if (hasActiveDispute)
                return ApiResponse<MentorDisputeResponse>.Fail("Lịch hẹn này đã có một đơn khiếu nại đang được xử lý.", HttpStatusCode.Conflict);

            var mentorId = slot?.MentorId ?? 0;
            if (mentorId <= 0)
                return ApiResponse<MentorDisputeResponse>.Fail("Lịch hẹn không có thông tin Chuyên gia hợp lệ.", HttpStatusCode.BadRequest);

            var dispute = new MentorDispute
            {
                MentorBookingId = booking.Id,
                LearnerId = claim.Id,
                MentorId = mentorId,
                Category = request.Category,
                Status = MentorDisputeStatus.Submitted,
                Reason = request.Reason.Trim(),
                EvidenceUrls = request.EvidenceUrls?.Trim(),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _context.MentorDisputes.Add(dispute);

            // Ghi nhật ký kiểm toán khởi tạo
            var history = new MentorDisputeHistory
            {
                Dispute = dispute,
                Action = "Created",
                PreviousStatus = null,
                NewStatus = MentorDisputeStatus.Submitted,
                ActorId = claim.Id,
                ActorRole = claim.Role.ToString(),
                Notes = "Học viên khởi tạo đơn khiếu nại",
                CreatedAtUtc = nowUtc
            };
            _context.MentorDisputeHistories.Add(history);

            // Ghi sự kiện Outbox phục vụ thông báo
            var outboxEvent = new MentorOutboxEvent
            {
                EventKey = $"dispute:submitted:{booking.Id}:{nowUtc.Ticks}",
                AggregateType = "MentorDispute",
                AggregateId = booking.Id.ToString(),
                EventType = "DisputeSubmitted",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    BookingId = booking.Id,
                    LearnerId = claim.Id,
                    MentorId = mentorId,
                    Category = dispute.Category.ToString(),
                    TimestampUtc = nowUtc
                }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            };
            _context.MentorOutboxEvents.Add(outboxEvent);

            await _context.SaveChangesAsync(cancellationToken);

            var learner = await _context.Users.FindAsync([claim.Id], cancellationToken);
            var mentor = await _context.Users.FindAsync([mentorId], cancellationToken);

            var response = MapToDisputeResponse(dispute, learner, mentor);
            return ApiResponse<MentorDisputeResponse>.Success(response, HttpStatusCode.OK, "Gửi khiếu nại thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi khiếu nại cho lịch hẹn {BookingId}: {Message}", request.MentorBookingId, ex.Message);
            return ApiResponse<MentorDisputeResponse>.Fail("Không thể gửi khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorDisputeResponse>>> GetLearnerDisputesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        try
        {
            var claim = _claimService.GetUserClaim();

            var disputes = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .Where(d => d.LearnerId == claim.Id)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => MapToDisputeResponse(d, d.Learner, d.Mentor))
                .ToListAsync(cancellationToken);

            return ApiResponse<List<MentorDisputeResponse>>.Success(disputes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách khiếu nại của học viên: {Message}", ex.Message);
            return ApiResponse<List<MentorDisputeResponse>>.Fail("Không thể tải danh sách khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorDisputeResponse>>> GetMentorDisputesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        try
        {
            var claim = _claimService.GetUserClaim();

            var disputes = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .Where(d => d.MentorId == claim.Id)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => MapToDisputeResponse(d, d.Learner, d.Mentor))
                .ToListAsync(cancellationToken);

            return ApiResponse<List<MentorDisputeResponse>>.Success(disputes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách khiếu nại của Mentor: {Message}", ex.Message);
            return ApiResponse<List<MentorDisputeResponse>>.Fail("Không thể tải danh sách khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorDisputeDetailResponse>> GetDisputeDetailAsync(
        int disputeId,
        CancellationToken cancellationToken = default)
    {
        if (disputeId <= 0)
            return ApiResponse<MentorDisputeDetailResponse>.Fail("Mã khiếu nại không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();

            var dispute = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .Include(d => d.MentorBooking)
                    .ThenInclude(b => b.MentorAvailability)
                .Include(d => d.Histories)
                .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

            if (dispute is null)
                return ApiResponse<MentorDisputeDetailResponse>.Fail("Không tìm thấy thông tin khiếu nại.", HttpStatusCode.NotFound);

            var isOwnerLearner = dispute.LearnerId == claim.Id;
            var isAssignedMentor = dispute.MentorId == claim.Id;
            var isAdmin = claim.Role == Role.Admin;

            if (!isOwnerLearner && !isAssignedMentor && !isAdmin)
                return ApiResponse<MentorDisputeDetailResponse>.Fail("Bạn không có quyền truy cập hồ sơ khiếu nại này.", HttpStatusCode.Forbidden);

            // Thu thập bằng chứng điểm danh từ Stage 09
            var attendance = await _context.MentorAttendanceRecords
                .Where(a => a.MentorBookingId == dispute.MentorBookingId && a.Role == "Learner")
                .FirstOrDefaultAsync(cancellationToken);

            // Kiểm tra trạng thái hoàn vé từ Stage 10
            var booking = dispute.MentorBooking;
            var isAlreadyRefunded = dispute.CreditRefunded ||
                                    (booking.CancelledAtUtc != null && booking.CancellationReason?.Contains("Học viên hủy muộn (< 24h)") == false) ||
                                    booking.ExceptionApproved == true;

            var histories = dispute.Histories
                .OrderBy(h => h.CreatedAtUtc)
                .Select(h => new MentorDisputeHistoryResponse
                {
                    Id = h.Id,
                    Action = h.Action,
                    PreviousStatus = h.PreviousStatus,
                    NewStatus = h.NewStatus,
                    ActorId = h.ActorId,
                    ActorRole = h.ActorRole,
                    Notes = h.Notes,
                    CreatedAtUtc = h.CreatedAtUtc
                })
                .ToList();

            var detail = new MentorDisputeDetailResponse
            {
                Dispute = MapToDisputeResponse(dispute, dispute.Learner, dispute.Mentor),
                BookingScheduledAt = booking.ScheduledAt,
                BookingStatus = booking.Status,
                AttendanceFirstJoinedAtUtc = attendance?.FirstJoinedAtUtc,
                AttendanceLastJoinedAtUtc = attendance?.LastJoinedAtUtc,
                AttendanceTotalDurationSeconds = attendance?.TotalDurationSeconds ?? 0,
                AttendanceReconnectionCount = attendance?.ReconnectionCount ?? 0,
                IsCancelled = booking.Status == MentorStatus.Cancelled,
                CancellationReason = booking.CancellationReason,
                CancelledByRole = booking.CancelledByRole,
                CreditAlreadyRefunded = isAlreadyRefunded,
                Histories = histories
            };

            return ApiResponse<MentorDisputeDetailResponse>.Success(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy chi tiết khiếu nại {DisputeId}: {Message}", disputeId, ex.Message);
            return ApiResponse<MentorDisputeDetailResponse>.Fail("Không thể tải chi tiết khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorDisputeResponse>> MentorRespondDisputeAsync(
        int disputeId,
        MentorResponseDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (disputeId <= 0)
            return ApiResponse<MentorDisputeResponse>.Fail("Mã khiếu nại không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.ResponseText))
            return ApiResponse<MentorDisputeResponse>.Fail("Nội dung giải trình không được để trống.");

        try
        {
            var claim = _claimService.GetUserClaim();

            var dispute = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

            if (dispute is null)
                return ApiResponse<MentorDisputeResponse>.Fail("Không tìm thấy thông tin khiếu nại.", HttpStatusCode.NotFound);

            if (dispute.MentorId != claim.Id && claim.Role != Role.Admin)
                return ApiResponse<MentorDisputeResponse>.Fail("Chỉ chuyên gia liên quan mới có quyền nộp giải trình.", HttpStatusCode.Forbidden);

            // Khiếu nại đã giải quyết hoặc đã đóng thì không thể giải trình thêm
            if (dispute.Status == MentorDisputeStatus.Resolved || dispute.Status == MentorDisputeStatus.Closed)
                return ApiResponse<MentorDisputeResponse>.Fail("Đơn khiếu nại đã kết thúc xử lý, không thể nộp thêm giải trình.", HttpStatusCode.Conflict);

            var nowUtc = DateTime.UtcNow;
            dispute.MentorResponse = request.ResponseText.Trim();
            dispute.MentorRespondedAtUtc = nowUtc;
            dispute.Status = MentorDisputeStatus.UnderReview;
            dispute.UpdatedAtUtc = nowUtc;

            var history = new MentorDisputeHistory
            {
                DisputeId = dispute.Id,
                Action = "MentorReplied",
                PreviousStatus = MentorDisputeStatus.AwaitingResponse,
                NewStatus = MentorDisputeStatus.UnderReview,
                ActorId = claim.Id,
                ActorRole = "Mentor",
                Notes = "Mentor nộp bản giải trình phản hồi",
                CreatedAtUtc = nowUtc
            };
            _context.MentorDisputeHistories.Add(history);

            _context.MentorDisputes.Update(dispute);
            await _context.SaveChangesAsync(cancellationToken);

            var response = MapToDisputeResponse(dispute, dispute.Learner, dispute.Mentor);
            return ApiResponse<MentorDisputeResponse>.Success(response, HttpStatusCode.OK, "Nộp giải trình khiếu nại thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi Mentor phản hồi khiếu nại {DisputeId}: {Message}", disputeId, ex.Message);
            return ApiResponse<MentorDisputeResponse>.Fail("Không thể nộp giải trình.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorDisputeResponse>>> AdminGetDisputeQueueAsync(
        MentorDisputeStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<List<MentorDisputeResponse>>.Fail("Chỉ Quản trị viên mới có quyền xem hàng đợi khiếu nại.", HttpStatusCode.Forbidden);

            var query = _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }

            var disputes = await query
                .OrderByDescending(d => d.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => MapToDisputeResponse(d, d.Learner, d.Mentor))
                .ToListAsync(cancellationToken);

            return ApiResponse<List<MentorDisputeResponse>>.Success(disputes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy hàng đợi khiếu nại Admin: {Message}", ex.Message);
            return ApiResponse<List<MentorDisputeResponse>>.Fail("Không thể tải hàng đợi khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorDisputeResponse>> AdminResolveDisputeAsync(
        int disputeId,
        ResolveMentorDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (disputeId <= 0)
            return ApiResponse<MentorDisputeResponse>.Fail("Mã khiếu nại không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<MentorDisputeResponse>.Fail("Chỉ Quản trị viên mới có quyền ra phán quyết khiếu nại.", HttpStatusCode.Forbidden);

            var dispute = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .Include(d => d.MentorBooking)
                    .ThenInclude(b => b.UserSubscription)
                .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

            if (dispute is null)
                return ApiResponse<MentorDisputeResponse>.Fail("Không tìm thấy thông tin khiếu nại.", HttpStatusCode.NotFound);

            if (dispute.Status == MentorDisputeStatus.Resolved || dispute.Status == MentorDisputeStatus.Closed)
                return ApiResponse<MentorDisputeResponse>.Fail("Đơn khiếu nại này đã được giải quyết trước đó.", HttpStatusCode.Conflict);

            var nowUtc = DateTime.UtcNow;
            var prevStatus = dispute.Status;
            var sub = dispute.MentorBooking?.UserSubscription;

            // Xử lý hoàn tín dụng nếu được duyệt
            if (request.ApproveRefund && sub != null)
            {
                // Kiểm tra xem credit đã được hoàn ở Stage 10 (hủy sớm hoặc ngoại lệ) chưa để tránh double-refund
                var alreadyRefundedInStage10 = dispute.MentorBooking.ExceptionApproved == true ||
                                               (dispute.MentorBooking.CancelledAtUtc != null && dispute.MentorBooking.CancellationReason?.Contains("Học viên hủy muộn (< 24h)") == false);

                if (dispute.CreditRefunded || alreadyRefundedInStage10)
                {
                    // Đã hoàn vé trước đó: ghi nhận phán quyết nhưng không trừ/hoàn lần hai
                    dispute.CreditRefunded = true;
                    dispute.AdminNotes = $"{request.AdminNotes} | Lưu ý: Lượt tín dụng đã được hoàn trả trong hệ thống trước đó, không thực hiện hoàn lại lần 2.";
                }
                else
                {
                    var opKey = $"refund:dispute:{dispute.Id}";
                    var refundResult = await _creditLedgerService.RefundCreditAsync(
                        dispute.LearnerId,
                        dispute.MentorBookingId,
                        opKey,
                        $"Hoàn tín dụng theo phán quyết khiếu nại #{dispute.Id}: {request.AdminDecision}",
                        cancellationToken);

                    if (!refundResult.IsSuccess && refundResult.ErrorMessage?.Contains("trước đó") != true)
                    {
                        return ApiResponse<MentorDisputeResponse>.Fail(
                            refundResult.ErrorMessage ?? "Không thể hoàn tín dụng qua Credit Ledger.", HttpStatusCode.Conflict);
                    }

                    dispute.CreditRefunded = true;
                    dispute.RefundOperationKey = opKey;

                    // Gia hạn thời hạn gói học của học viên
                    var extensionDays = request.ExtendDays > 0 ? request.ExtendDays : 7;
                    if (sub.EndDate < nowUtc.AddDays(extensionDays))
                    {
                        sub.EndDate = nowUtc.AddDays(extensionDays);
                        _context.UserSubscriptions.Update(sub);
                    }
                }
            }

            dispute.Status = request.ApproveRefund ? MentorDisputeStatus.Resolved : MentorDisputeStatus.Rejected;
            dispute.IsRefundApproved = request.ApproveRefund;
            dispute.AdminDecision = request.AdminDecision;
            dispute.AdminNotes = string.IsNullOrWhiteSpace(dispute.AdminNotes) ? request.AdminNotes : dispute.AdminNotes;
            dispute.ResolvedByAdminId = claim.Id;
            dispute.ResolvedAtUtc = nowUtc;
            dispute.UpdatedAtUtc = nowUtc;

            // Ghi nhật ký kiểm toán
            var history = new MentorDisputeHistory
            {
                DisputeId = dispute.Id,
                Action = "AdminResolved",
                PreviousStatus = prevStatus,
                NewStatus = dispute.Status,
                ActorId = claim.Id,
                ActorRole = "Admin",
                Notes = $"Quyết định: {request.AdminDecision} | Hoàn vé: {request.ApproveRefund}",
                CreatedAtUtc = nowUtc
            };
            _context.MentorDisputeHistories.Add(history);

            // Ghi sự kiện Outbox
            var outboxEvent = new MentorOutboxEvent
            {
                EventKey = $"dispute:resolved:{dispute.Id}:{nowUtc.Ticks}",
                AggregateType = "MentorDispute",
                AggregateId = dispute.Id.ToString(),
                EventType = "DisputeResolved",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    DisputeId = dispute.Id,
                    BookingId = dispute.MentorBookingId,
                    LearnerId = dispute.LearnerId,
                    MentorId = dispute.MentorId,
                    IsRefundApproved = request.ApproveRefund,
                    Decision = request.AdminDecision,
                    TimestampUtc = nowUtc
                }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            };
            _context.MentorOutboxEvents.Add(outboxEvent);

            _context.MentorDisputes.Update(dispute);
            await _context.SaveChangesAsync(cancellationToken);

            var response = MapToDisputeResponse(dispute, dispute.Learner, dispute.Mentor);
            return ApiResponse<MentorDisputeResponse>.Success(response, HttpStatusCode.OK, "Giải quyết khiếu nại thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi Admin giải quyết khiếu nại {DisputeId}: {Message}", disputeId, ex.Message);
            return ApiResponse<MentorDisputeResponse>.Fail("Không thể giải quyết khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorDisputeResponse>> AdminUpdateDisputeStatusAsync(
        int disputeId,
        UpdateDisputeStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (disputeId <= 0)
            return ApiResponse<MentorDisputeResponse>.Fail("Mã khiếu nại không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<MentorDisputeResponse>.Fail("Chỉ Quản trị viên mới có quyền chuyển đổi trạng thái khiếu nại.", HttpStatusCode.Forbidden);

            var dispute = await _context.MentorDisputes
                .Include(d => d.Learner)
                .Include(d => d.Mentor)
                .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

            if (dispute is null)
                return ApiResponse<MentorDisputeResponse>.Fail("Không tìm thấy thông tin khiếu nại.", HttpStatusCode.NotFound);

            var nowUtc = DateTime.UtcNow;
            var prevStatus = dispute.Status;

            dispute.Status = request.NewStatus;
            dispute.UpdatedAtUtc = nowUtc;

            var history = new MentorDisputeHistory
            {
                DisputeId = dispute.Id,
                Action = "StatusChanged",
                PreviousStatus = prevStatus,
                NewStatus = request.NewStatus,
                ActorId = claim.Id,
                ActorRole = "Admin",
                Notes = request.Notes,
                CreatedAtUtc = nowUtc
            };
            _context.MentorDisputeHistories.Add(history);

            _context.MentorDisputes.Update(dispute);
            await _context.SaveChangesAsync(cancellationToken);

            var response = MapToDisputeResponse(dispute, dispute.Learner, dispute.Mentor);
            return ApiResponse<MentorDisputeResponse>.Success(response, HttpStatusCode.OK, "Cập nhật trạng thái khiếu nại thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật trạng thái khiếu nại {DisputeId}: {Message}", disputeId, ex.Message);
            return ApiResponse<MentorDisputeResponse>.Fail("Không thể cập nhật trạng thái khiếu nại.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorQualityMetricResponse>>> AdminGetQualityMetricsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
                return ApiResponse<List<MentorQualityMetricResponse>>.Fail("Chỉ Quản trị viên mới có quyền xem báo cáo chất lượng.", HttpStatusCode.Forbidden);

            var mentors = await _context.Users
                .Where(u => u.Role == Role.Mentor && !u.IsDeleted)
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var mentorIds = mentors.Select(m => m.Id).ToList();

            var bookings = await _context.MentorBookings
                .Include(b => b.MentorAvailability)
                .Where(b => b.MentorAvailability != null && mentorIds.Contains(b.MentorAvailability.MentorId) && !b.IsDeleted)
                .Select(b => new { MentorId = b.MentorAvailability!.MentorId, b.Status })
                .ToListAsync(cancellationToken);

            var reviews = await _context.MentorReviews
                .Where(r => mentorIds.Contains(r.MentorId) && r.IsPublic && r.ModerationStatus == MentorReviewStatus.Approved)
                .Select(r => new { r.MentorId, r.Rating })
                .ToListAsync(cancellationToken);

            var disputes = await _context.MentorDisputes
                .Where(d => mentorIds.Contains(d.MentorId))
                .Select(d => new { d.MentorId, d.Status, d.IsRefundApproved })
                .ToListAsync(cancellationToken);

            var metrics = new List<MentorQualityMetricResponse>();

            foreach (var mentor in mentors)
            {
                var mBookings = bookings.Where(b => b.MentorId == mentor.Id).ToList();
                var mReviews = reviews.Where(r => r.MentorId == mentor.Id).ToList();
                var mDisputes = disputes.Where(d => d.MentorId == mentor.Id).ToList();

                var completedCount = mBookings.Count(b => b.Status == MentorStatus.Completed);
                var cancelledCount = mBookings.Count(b => b.Status == MentorStatus.Cancelled);
                var totalSessions = completedCount + cancelledCount;

                var totalReviews = mReviews.Count;
                var avgRating = totalReviews > 0 ? Math.Round(mReviews.Average(r => r.Rating), 1) : 0.0;

                var totalDisputes = mDisputes.Count;
                var disputeRate = totalSessions > 0 ? Math.Round((double)totalDisputes * 100 / totalSessions, 1) : 0.0;
                var refundCount = mDisputes.Count(d => d.IsRefundApproved == true);

                metrics.Add(new MentorQualityMetricResponse
                {
                    MentorId = mentor.Id,
                    MentorName = $"{mentor.FirstName} {mentor.LastName}".Trim(),
                    CompletedSessionsCount = completedCount,
                    CancelledSessionsCount = cancelledCount,
                    TotalReviewsCount = totalReviews,
                    AverageRating = avgRating,
                    TotalDisputesCount = totalDisputes,
                    DisputeRatePercent = disputeRate,
                    ResolvedRefundCount = refundCount
                });
            }

            return ApiResponse<List<MentorQualityMetricResponse>>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tổng hợp báo cáo chỉ số chất lượng: {Message}", ex.Message);
            return ApiResponse<List<MentorQualityMetricResponse>>.Fail("Không thể tải báo cáo chất lượng.", HttpStatusCode.InternalServerError);
        }
    }

    private static MentorDisputeResponse MapToDisputeResponse(MentorDispute d, UserAccount? learner, UserAccount? mentor)
    {
        return new MentorDisputeResponse
        {
            Id = d.Id,
            MentorBookingId = d.MentorBookingId,
            LearnerId = d.LearnerId,
            LearnerName = learner != null ? $"{learner.FirstName} {learner.LastName}".Trim() : string.Empty,
            MentorId = d.MentorId,
            MentorName = mentor != null ? $"{mentor.FirstName} {mentor.LastName}".Trim() : string.Empty,
            Category = d.Category,
            Status = d.Status,
            Reason = d.Reason,
            EvidenceUrls = d.EvidenceUrls,
            MentorResponse = d.MentorResponse,
            MentorRespondedAtUtc = d.MentorRespondedAtUtc,
            AdminNotes = d.AdminNotes,
            AdminDecision = d.AdminDecision,
            IsRefundApproved = d.IsRefundApproved,
            CreditRefunded = d.CreditRefunded,
            RefundOperationKey = d.RefundOperationKey,
            ResolvedByAdminId = d.ResolvedByAdminId,
            ResolvedAtUtc = d.ResolvedAtUtc,
            CreatedAtUtc = d.CreatedAtUtc,
            UpdatedAtUtc = d.UpdatedAtUtc
        };
    }
}
