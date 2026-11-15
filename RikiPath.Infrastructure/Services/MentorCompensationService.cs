using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorCompensation;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorCompensation;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorCompensationService : IMentorCompensationService
{
    private readonly AppDbContext _context;
    private readonly IClaimService _claimService;
    private readonly IPayOsPayoutClient _payOsPayoutClient;
    private readonly AppSettings _appSettings;
    private readonly ILogger<MentorCompensationService> _logger;

    public MentorCompensationService(
        AppDbContext context,
        IClaimService claimService,
        IPayOsPayoutClient payOsPayoutClient,
        AppSettings appSettings,
        ILogger<MentorCompensationService> logger)
    {
        _context = context;
        _claimService = claimService;
        _payOsPayoutClient = payOsPayoutClient;
        _appSettings = appSettings;
        _logger = logger;
    }

    // =========================================================================
    // 1. HOURLY RATE & JLPT COEFFICIENT CONFIGURATION
    // =========================================================================

    public async Task<ApiResponse<MentorHourlyRateResponse>> ConfigureMentorHourlyRateAsync(
        ConfigureMentorHourlyRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorHourlyRateResponse>.Fail("Chỉ Quản trị viên mới có quyền cấu hình đơn giá thù lao giờ.", HttpStatusCode.Forbidden);

        if (request.MentorUserId <= 0)
            return ApiResponse<MentorHourlyRateResponse>.Fail("Mã cố vấn không hợp lệ.");

        if (request.HourlyRateVnd <= 0 || decimal.Truncate(request.HourlyRateVnd) != request.HourlyRateVnd)
            return ApiResponse<MentorHourlyRateResponse>.Fail("Đơn giá thù lao theo giờ phải là số tiền VND nguyên dương.");

        var mentor = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.MentorUserId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);
        if (mentor is null)
            return ApiResponse<MentorHourlyRateResponse>.Fail("Không tìm thấy tài khoản cố vấn hợp lệ.", HttpStatusCode.NotFound);

        var nowUtc = DateTime.UtcNow;
        var effectiveFrom = NormalizeUtc(request.EffectiveFromUtc ?? nowUtc);
        var effectiveUntil = request.EffectiveToUtc.HasValue ? NormalizeUtc(request.EffectiveToUtc.Value) : (DateTime?)null;

        if (effectiveUntil.HasValue && effectiveUntil.Value <= effectiveFrom)
            return ApiResponse<MentorHourlyRateResponse>.Fail("Thời điểm kết thúc hiệu lực phải sau thời điểm bắt đầu hiệu lực.");

        var existingRates = await _context.MentorHourlyRates
            .Where(r => r.MentorUserId == request.MentorUserId)
            .OrderBy(r => r.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingRates)
        {
            if (existing.EffectiveUntilUtc == null && existing.EffectiveFromUtc < effectiveFrom)
            {
                existing.EffectiveUntilUtc = effectiveFrom;
                _context.MentorHourlyRates.Update(existing);
                continue;
            }

            var existingEnd = existing.EffectiveUntilUtc ?? DateTime.MaxValue;
            var newEnd = effectiveUntil ?? DateTime.MaxValue;
            if (existing.EffectiveFromUtc < newEnd && effectiveFrom < existingEnd)
            {
                return ApiResponse<MentorHourlyRateResponse>.Fail(
                    "Khoảng thời gian hiệu lực của đơn giá giờ bị chồng lấn với cấu hình đang hoạt động.",
                    HttpStatusCode.Conflict);
            }
        }

        var rate = new MentorHourlyRate
        {
            MentorUserId = request.MentorUserId,
            AmountPerHour = request.HourlyRateVnd,
            EffectiveFromUtc = effectiveFrom,
            EffectiveUntilUtc = effectiveUntil,
            ConfiguredByAdminId = claim.Id,
            CreatedAtUtc = nowUtc
        };

        _context.MentorHourlyRates.Add(rate);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<MentorHourlyRateResponse>.Success(
            MapHourlyRate(rate, mentor),
            HttpStatusCode.Created,
            "Cấu hình đơn giá thù lao giờ cho cố vấn thành công.");
    }

    public async Task<ApiResponse<List<MentorHourlyRateResponse>>> GetMentorHourlyRatesAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<List<MentorHourlyRateResponse>>.Fail("Học viên không có quyền xem cấu hình thù lao nội bộ của cố vấn.", HttpStatusCode.Forbidden);

        if (claim.Role == Role.Mentor)
        {
            if (mentorUserId.HasValue && mentorUserId.Value != claim.Id)
                return ApiResponse<List<MentorHourlyRateResponse>>.Fail("Cố vấn chỉ được xem đơn giá thù lao của chính mình.", HttpStatusCode.Forbidden);
            mentorUserId = claim.Id;
        }
        else if (claim.Role != Role.Admin)
        {
            return ApiResponse<List<MentorHourlyRateResponse>>.Fail("Truy cập bị từ chối.", HttpStatusCode.Forbidden);
        }

        var query = _context.MentorHourlyRates
            .Include(r => r.MentorUser)
            .AsQueryable();

        if (mentorUserId.HasValue)
            query = query.Where(r => r.MentorUserId == mentorUserId.Value);

        var rows = await query
            .OrderBy(r => r.MentorUserId)
            .ThenByDescending(r => r.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorHourlyRateResponse>>.Success(
            rows.Select(r => MapHourlyRate(r, r.MentorUser)).ToList());
    }

    public async Task<ApiResponse<JlptLevelPayCoefficientResponse>> ConfigureJlptCoefficientAsync(
        ConfigureJlptCoefficientRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<JlptLevelPayCoefficientResponse>.Fail("Chỉ Quản trị viên mới có quyền cấu hình hệ số trình độ JLPT.", HttpStatusCode.Forbidden);

        if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out var normalizedLevel))
            return ApiResponse<JlptLevelPayCoefficientResponse>.Fail(
                "Hệ số thù lao cố vấn chỉ hỗ trợ các trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).",
                HttpStatusCode.BadRequest);

        if (request.Coefficient <= 0 || request.Coefficient > 20m)
            return ApiResponse<JlptLevelPayCoefficientResponse>.Fail("Hệ số trình độ phải lớn hơn 0 và hợp lệ.");

        var levelId = await ResolveCertificateLevelIdAsync(normalizedLevel, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var effectiveFrom = NormalizeUtc(request.EffectiveFromUtc ?? nowUtc);
        var effectiveUntil = request.EffectiveToUtc.HasValue ? NormalizeUtc(request.EffectiveToUtc.Value) : (DateTime?)null;

        if (effectiveUntil.HasValue && effectiveUntil.Value <= effectiveFrom)
            return ApiResponse<JlptLevelPayCoefficientResponse>.Fail("Thời điểm kết thúc hiệu lực phải sau thời điểm bắt đầu hiệu lực.");

        var existingCoeffs = await _context.JlptLevelPayCoefficients
            .Where(c => c.CertificateLevelId == levelId)
            .OrderBy(c => c.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingCoeffs)
        {
            if (existing.EffectiveUntilUtc == null && existing.EffectiveFromUtc < effectiveFrom)
            {
                existing.EffectiveUntilUtc = effectiveFrom;
                _context.JlptLevelPayCoefficients.Update(existing);
                continue;
            }

            var existingEnd = existing.EffectiveUntilUtc ?? DateTime.MaxValue;
            var newEnd = effectiveUntil ?? DateTime.MaxValue;
            if (existing.EffectiveFromUtc < newEnd && effectiveFrom < existingEnd)
            {
                return ApiResponse<JlptLevelPayCoefficientResponse>.Fail(
                    $"Khoảng thời gian hiệu lực của hệ số {normalizedLevel} bị chồng lấn với cấu hình đang hoạt động.",
                    HttpStatusCode.Conflict);
            }
        }

        var coeff = new JlptLevelPayCoefficient
        {
            CertificateLevelId = levelId,
            Coefficient = decimal.Round(request.Coefficient, 4, MidpointRounding.AwayFromZero),
            EffectiveFromUtc = effectiveFrom,
            EffectiveUntilUtc = effectiveUntil,
            ConfiguredByAdminId = claim.Id,
            CreatedAtUtc = nowUtc
        };

        _context.JlptLevelPayCoefficients.Add(coeff);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<JlptLevelPayCoefficientResponse>.Success(
            MapCoefficient(coeff, normalizedLevel),
            HttpStatusCode.Created,
            $"Cấu hình hệ số thù lao cho trình độ {normalizedLevel} thành công.");
    }

    public async Task<ApiResponse<List<JlptLevelPayCoefficientResponse>>> GetJlptCoefficientsAsync(
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role is not (Role.Admin or Role.Mentor))
            return ApiResponse<List<JlptLevelPayCoefficientResponse>>.Fail("Bạn không có quyền xem hệ số thù lao.", HttpStatusCode.Forbidden);

        var rows = await _context.JlptLevelPayCoefficients
            .Include(c => c.CertificateLevel)
            .OrderBy(c => c.CertificateLevelId)
            .ThenByDescending(c => c.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<JlptLevelPayCoefficientResponse>>.Success(
            rows.Select(c => MapCoefficient(c, c.CertificateLevel?.Code ?? ResolveLevelCode(c.CertificateLevelId))).ToList());
    }

    // =========================================================================
    // 2. MENTOR TEACHING QUALIFICATIONS (N5–N2 ONLY)
    // =========================================================================

    public async Task<ApiResponse<MentorTeachingQualificationResponse>> GrantTeachingQualificationAsync(
        GrantMentorTeachingQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Chỉ Quản trị viên mới có quyền cấp quyền giảng dạy trình độ JLPT.", HttpStatusCode.Forbidden);

        if (request.MentorUserId <= 0)
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Mã cố vấn không hợp lệ.");

        if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out var normalizedLevel))
            return ApiResponse<MentorTeachingQualificationResponse>.Fail(
                "Cố vấn chỉ được cấp quyền giảng dạy các trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).",
                HttpStatusCode.BadRequest);

        var mentor = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.MentorUserId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);
        if (mentor is null)
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Không tìm thấy tài khoản cố vấn.", HttpStatusCode.NotFound);

        var levelId = await ResolveCertificateLevelIdAsync(normalizedLevel, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var existing = await _context.MentorTeachingQualifications
            .FirstOrDefaultAsync(q => q.MentorUserId == request.MentorUserId && q.CertificateLevelId == levelId, cancellationToken);

        if (existing is null)
        {
            existing = new MentorTeachingQualification
            {
                MentorUserId = request.MentorUserId,
                CertificateLevelId = levelId,
                IsActive = true,
                VerifiedByAdminId = claim.Id,
                VerifiedAtUtc = nowUtc,
                RevokedAtUtc = null
            };
            _context.MentorTeachingQualifications.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.VerifiedByAdminId = claim.Id;
            existing.VerifiedAtUtc = nowUtc;
            existing.RevokedAtUtc = null;
            _context.MentorTeachingQualifications.Update(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<MentorTeachingQualificationResponse>.Success(
            MapQualification(existing, mentor, normalizedLevel),
            HttpStatusCode.OK,
            $"Đã cấp quyền giảng dạy trình độ {normalizedLevel} cho cố vấn.");
    }

    public async Task<ApiResponse<MentorTeachingQualificationResponse>> RevokeTeachingQualificationAsync(
        RevokeMentorTeachingQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Chỉ Quản trị viên mới có quyền thu hồi quyền giảng dạy.", HttpStatusCode.Forbidden);

        if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out var normalizedLevel))
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Trình độ JLPT không hợp lệ.", HttpStatusCode.BadRequest);

        var levelId = await ResolveCertificateLevelIdAsync(normalizedLevel, cancellationToken);
        var existing = await _context.MentorTeachingQualifications
            .Include(q => q.MentorUser)
            .FirstOrDefaultAsync(q => q.MentorUserId == request.MentorUserId && q.CertificateLevelId == levelId, cancellationToken);

        if (existing is null)
            return ApiResponse<MentorTeachingQualificationResponse>.Fail("Không tìm thấy quyền giảng dạy tương ứng.", HttpStatusCode.NotFound);

        var nowUtc = DateTime.UtcNow;
        existing.IsActive = false;
        existing.RevokedAtUtc = nowUtc >= existing.VerifiedAtUtc ? nowUtc : existing.VerifiedAtUtc;
        _context.MentorTeachingQualifications.Update(existing);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<MentorTeachingQualificationResponse>.Success(
            MapQualification(existing, existing.MentorUser, normalizedLevel),
            HttpStatusCode.OK,
            $"Đã thu hồi quyền giảng dạy trình độ {normalizedLevel}.");
    }

    public async Task<ApiResponse<List<MentorTeachingQualificationResponse>>> GetMentorQualificationsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<List<MentorTeachingQualificationResponse>>.Fail("Học viên không có quyền truy cập mục này.", HttpStatusCode.Forbidden);

        if (claim.Role == Role.Mentor)
        {
            if (mentorUserId.HasValue && mentorUserId.Value != claim.Id)
                return ApiResponse<List<MentorTeachingQualificationResponse>>.Fail("Cố vấn chỉ được xem quyền giảng dạy của chính mình.", HttpStatusCode.Forbidden);
            mentorUserId = claim.Id;
        }

        var query = _context.MentorTeachingQualifications
            .Include(q => q.MentorUser)
            .Include(q => q.CertificateLevel)
            .AsQueryable();

        if (mentorUserId.HasValue)
            query = query.Where(q => q.MentorUserId == mentorUserId.Value);

        var rows = await query
            .OrderBy(q => q.MentorUserId)
            .ThenBy(q => q.CertificateLevelId)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorTeachingQualificationResponse>>.Success(
            rows.Select(q => MapQualification(q, q.MentorUser, q.CertificateLevel?.Code ?? ResolveLevelCode(q.CertificateLevelId))).ToList());
    }

    // =========================================================================
    // 3. ATTENDANCE APPROVAL & IMMUTABLE EARNING LEDGER
    // =========================================================================

    public async Task<ApiResponse<MentorAttendanceApprovalResponse>> EvaluateBookingAttendanceAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Chỉ Quản trị viên mới có quyền đánh giá điểm danh để ghi nhận thù lao.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(
            ct => EvaluateOrAdjudicateAttendanceInternalAsync(bookingId, isAdminOverride: false, null, claim.Id, ct),
            cancellationToken);
    }

    public async Task<ApiResponse<MentorAttendanceApprovalResponse>> ReviewBookingAttendanceAsync(
        int bookingId,
        ReviewBookingAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Chỉ Quản trị viên mới có quyền phê duyệt điểm danh và thù lao buổi tư vấn.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(
            ct => EvaluateOrAdjudicateAttendanceInternalAsync(bookingId, isAdminOverride: true, request, claim.Id, ct),
            cancellationToken);
    }

    private async Task<ApiResponse<MentorAttendanceApprovalResponse>> EvaluateOrAdjudicateAttendanceInternalAsync(
        int bookingId,
        bool isAdminOverride,
        ReviewBookingAttendanceRequest? request,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        if (bookingId <= 0)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Mã lịch hẹn không hợp lệ.");

        var booking = await _context.MentorBookings
            .Include(b => b.MentorAvailability)
            .Include(b => b.UserSubscription)
            .Include(b => b.CompensationLevel)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

        if (booking is null)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Không tìm thấy lịch hẹn tư vấn.", HttpStatusCode.NotFound);

        var mentorUserId = booking.MentorAvailability?.MentorId ?? booking.UserSubscription?.MentorId ?? 0;
        var learnerUserId = booking.UserSubscription?.UserId ?? 0;
        if (mentorUserId <= 0 || learnerUserId <= 0)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Lịch hẹn thiếu thông tin cố vấn hoặc học viên.", HttpStatusCode.BadRequest);

        var nowUtc = DateTime.UtcNow;

        var attendanceRecords = await _context.MentorAttendanceRecords
            .Where(r => r.MentorBookingId == bookingId)
            .ToListAsync(cancellationToken);

        var mentorRecord = attendanceRecords.FirstOrDefault(r => r.UserId == mentorUserId || r.Role == "Mentor");
        var learnerRecord = attendanceRecords.FirstOrDefault(r => r.UserId == learnerUserId || r.Role == "Learner");
        int rawMentorSeconds = Math.Max(0, mentorRecord?.TotalDurationSeconds ?? 0);
        int rawLearnerSeconds = Math.Max(0, learnerRecord?.TotalDurationSeconds ?? 0);

        var approval = await _context.MentorAttendanceApprovals
            .FirstOrDefaultAsync(a => a.MentorBookingId == bookingId, cancellationToken);

        var existingEarning = await _context.MentorEarningEntries
            .Include(e => e.CertificateLevel)
            .FirstOrDefaultAsync(e => e.MentorBookingId == bookingId, cancellationToken);

        if (existingEarning is not null && existingEarning.Status is MentorEarningStatus.LockedInPayroll or MentorEarningStatus.Paid)
        {
            if (approval is not null)
            {
                return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                    MapAttendanceApproval(approval, booking, existingEarning),
                    message: "Bút toán thù lao đã được khóa trong bảng lương hoặc chi trả (idempotent).");
            }
        }

        // CHÍNH SÁCH LỊCH SỬ (Legacy Null-Snapshot Policy):
        // Không bao giờ tự suy đoán trình độ JLPT hoặc đơn giá cho các booking lịch sử thiếu snapshot đóng băng!
        if (!booking.CompensationLevelId.HasValue ||
            !booking.FrozenHourlyRate.HasValue ||
            !booking.FrozenLevelCoefficient.HasValue ||
            !booking.ScheduledCompensationAmount.HasValue)
        {
            var legacyReason = "Lịch hẹn lịch sử không có snapshot trình độ JLPT hoặc đơn giá đóng băng (LegacyIneligible); tuyệt đối không tự suy đoán đơn giá và không tự động tạo bút toán thù lao.";
            approval = UpsertApprovalEntity(
                approval, bookingId,
                rawMentorSeconds, rawLearnerSeconds,
                approvedEligibleMinutes: null,
                MentorAttendanceApprovalStatus.LegacyIneligible,
                legacyReason, adminUserId, nowUtc);

            await _context.SaveChangesAsync(cancellationToken);

            if (isAdminOverride && request?.Approve == true)
            {
                return ApiResponse<MentorAttendanceApprovalResponse>.Fail(
                    legacyReason,
                    HttpStatusCode.Conflict);
            }

            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, null),
                message: legacyReason);
        }

        // Kiểm tra khiếu nại đang mở (Open Dispute Hold Policy)
        var hasOpenDispute = await _context.MentorDisputes
            .AnyAsync(d => d.MentorBookingId == bookingId
                && d.Status != MentorDisputeStatus.Closed
                && d.Status != MentorDisputeStatus.Rejected
                && !(d.Status == MentorDisputeStatus.Resolved && d.IsRefundApproved == false), cancellationToken);

        if ((request?.HoldDisputed == true) || (hasOpenDispute && (!isAdminOverride || request?.Approve != true)))
        {
            var holdReason = request?.Reason?.Trim() ?? "Lịch hẹn đang có khiếu nại mở hoặc bị tạm giữ để điều tra chất lượng (HeldDisputed).";
            approval = UpsertApprovalEntity(
                approval, bookingId,
                rawMentorSeconds, rawLearnerSeconds,
                approvedEligibleMinutes: null,
                MentorAttendanceApprovalStatus.HeldDisputed,
                holdReason, adminUserId, nowUtc);

            if (existingEarning is not null && existingEarning.Status == MentorEarningStatus.Eligible)
            {
                existingEarning.Status = MentorEarningStatus.HeldDisputed;
                _context.MentorEarningEntries.Update(existingEarning);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, existingEarning),
                message: "Đã tạm giữ thù lao do đang có khiếu nại hoặc chờ điều tra (HeldDisputed).");
        }

        var hasCompleteEvidence = mentorRecord is not null && learnerRecord is not null
            && rawMentorSeconds >= 60 && rawLearnerSeconds >= 60 && !hasOpenDispute;

        if (!isAdminOverride && !hasCompleteEvidence)
        {
            var reviewReason = "Bằng chứng điểm danh phòng tư vấn chưa đầy đủ cho cả Cố vấn và Học viên; chuyển sang trạng thái chờ Quản trị viên xét duyệt (PendingAdminReview).";
            approval = UpsertApprovalEntity(
                approval, bookingId,
                rawMentorSeconds, rawLearnerSeconds,
                approvedEligibleMinutes: null,
                MentorAttendanceApprovalStatus.PendingAdminReview,
                reviewReason, adminUserId, nowUtc);

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, existingEarning),
                message: reviewReason);
        }

        // Xử lý từ chối thù lao
        if (isAdminOverride && request is not null && !request.Approve)
        {
            var rejectReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Quản trị viên từ chối ghi nhận thù lao cho buổi tư vấn này."
                : request.Reason.Trim();

            approval = UpsertApprovalEntity(
                approval, bookingId,
                rawMentorSeconds, rawLearnerSeconds,
                approvedEligibleMinutes: null,
                MentorAttendanceApprovalStatus.RejectedNoEarning,
                rejectReason, adminUserId, nowUtc);

            if (existingEarning is not null && existingEarning.Status == MentorEarningStatus.Eligible)
            {
                existingEarning.Status = MentorEarningStatus.HeldDisputed;
                _context.MentorEarningEntries.Update(existingEarning);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, existingEarning),
                message: "Đã từ chối ghi nhận thù lao cho buổi tư vấn.");
        }

        // Xử lý phê duyệt thù lao (ApprovedForEarning)
        int approvedMinutes;
        if (isAdminOverride && request is not null)
        {
            approvedMinutes = request.ApprovedEligibleMinutes;
            if (!hasCompleteEvidence && string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponse<MentorAttendanceApprovalResponse>.Fail(
                    "Bằng chứng điểm danh chưa đầy đủ; Quản trị viên bắt buộc nhập lý do xác minh khi phê duyệt thủ công.",
                    HttpStatusCode.BadRequest);
            }
        }
        else
        {
            var observedMinutes = Math.Min(rawMentorSeconds, rawLearnerSeconds) / 60;
            approvedMinutes = Math.Clamp(observedMinutes > 0 ? observedMinutes : 90, 1, MentorSlotPolicy.AppointmentDurationMinutes);
        }

        if (approvedMinutes < 1 || approvedMinutes > MentorSlotPolicy.AppointmentDurationMinutes)
        {
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail(
                $"Số phút hợp lệ được duyệt phải từ 1 đến {MentorSlotPolicy.AppointmentDurationMinutes} phút.",
                HttpStatusCode.BadRequest);
        }

        var approveReason = request?.Reason?.Trim() ?? "Điểm danh hợp lệ; phê duyệt ghi nhận vào sổ cái thù lao.";
        approval = UpsertApprovalEntity(
            approval, bookingId,
            rawMentorSeconds, rawLearnerSeconds,
            approvedMinutes,
            MentorAttendanceApprovalStatus.ApprovedForEarning,
            approveReason, adminUserId, nowUtc);

        await _context.SaveChangesAsync(cancellationToken);

        // Idempotent: nếu MentorEarningEntry cho booking này đã tồn tại thì không tạo bản ghi thứ 2!
        if (existingEarning is not null)
        {
            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, existingEarning),
                message: "Bút toán thù lao cho buổi tư vấn này đã tồn tại (idempotent).");
        }

        var eligibleAmount = MentorCompensationPolicy.CalculateEligibleEarning(
            booking.FrozenHourlyRate.Value,
            booking.FrozenLevelCoefficient.Value,
            approvedMinutes);

        var opKey = !string.IsNullOrWhiteSpace(request?.OperationKey)
            ? request.OperationKey.Trim()
            : $"earning:booking:{bookingId}";

        var earningByOpKey = await _context.MentorEarningEntries
            .FirstOrDefaultAsync(e => e.OperationKey == opKey, cancellationToken);
        if (earningByOpKey is not null)
        {
            return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                MapAttendanceApproval(approval, booking, earningByOpKey),
                message: "Bút toán thù lao đã được tạo trước đó theo OperationKey (idempotent).");
        }

        var levelCode = booking.CompensationLevel?.Code ?? ResolveLevelCode(booking.CompensationLevelId.Value);

        var newEarning = new MentorEarningEntry
        {
            MentorUserId = mentorUserId,
            MentorBookingId = bookingId,
            AttendanceApprovalId = approval.Id,
            CertificateLevelId = booking.CompensationLevelId.Value,
            JlptLevelCode = levelCode,
            FrozenHourlyRate = booking.FrozenHourlyRate.Value,
            FrozenLevelCoefficient = booking.FrozenLevelCoefficient.Value,
            ApprovedEligibleMinutes = approvedMinutes,
            GrossAmount = eligibleAmount,
            Currency = booking.CompensationCurrency ?? "VND",
            FormulaVersion = booking.CompensationFormulaVersion ?? MentorCompensationPolicy.FormulaVersion,
            Status = MentorEarningStatus.Eligible,
            OperationKey = opKey,
            CreatedByAdminId = adminUserId,
            CreatedAtUtc = nowUtc
        };

        try
        {
            _context.MentorEarningEntries.Add(newEarning);
            if (booking.Status is MentorStatus.Accepted or MentorStatus.Assigned)
            {
                booking.Status = MentorStatus.Completed;
                _context.MentorBookings.Update(booking);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate MentorEarningEntry prevented for booking {BookingId}.", bookingId);
            _context.Entry(newEarning).State = EntityState.Detached;
            var reloadedEarning = await _context.MentorEarningEntries
                .FirstOrDefaultAsync(e => e.MentorBookingId == bookingId, cancellationToken);
            if (reloadedEarning is not null)
            {
                return ApiResponse<MentorAttendanceApprovalResponse>.Success(
                    MapAttendanceApproval(approval, booking, reloadedEarning),
                    message: "Bút toán thù lao đã được ghi nhận bởi giao dịch đồng thời (idempotent).");
            }
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Xung đột dữ liệu khi tạo bút toán thù lao.", HttpStatusCode.Conflict);
        }

        return ApiResponse<MentorAttendanceApprovalResponse>.Success(
            MapAttendanceApproval(approval, booking, newEarning, levelCode),
            HttpStatusCode.OK,
            "Phê duyệt điểm danh và ghi sổ cái thù lao cố vấn thành công.");
    }

    public async Task<ApiResponse<MentorAttendanceApprovalResponse>> GetBookingAttendanceApprovalAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Học viên không có quyền xem xét duyệt thù lao của cố vấn.", HttpStatusCode.Forbidden);

        var approval = await _context.MentorAttendanceApprovals
            .Include(a => a.MentorBooking)
                .ThenInclude(b => b.MentorAvailability)
            .Include(a => a.MentorBooking)
                .ThenInclude(b => b.UserSubscription)
            .FirstOrDefaultAsync(a => a.MentorBookingId == bookingId, cancellationToken);

        if (approval is null)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Chưa có bản ghi xét duyệt điểm danh thù lao cho lịch hẹn này.", HttpStatusCode.NotFound);

        var mentorUserId = approval.MentorBooking?.MentorAvailability?.MentorId
            ?? approval.MentorBooking?.UserSubscription?.MentorId ?? 0;

        if (claim.Role == Role.Mentor && mentorUserId != claim.Id)
            return ApiResponse<MentorAttendanceApprovalResponse>.Fail("Bạn không có quyền xem dữ liệu của cố vấn khác.", HttpStatusCode.Forbidden);

        var earning = await _context.MentorEarningEntries
            .Include(e => e.CertificateLevel)
            .FirstOrDefaultAsync(e => e.MentorBookingId == bookingId, cancellationToken);

        return ApiResponse<MentorAttendanceApprovalResponse>.Success(
            MapAttendanceApproval(approval, approval.MentorBooking, earning));
    }

    public async Task<ApiResponse<List<MentorEarningEntryResponse>>> GetMentorEarningsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<List<MentorEarningEntryResponse>>.Fail("Học viên không có quyền xem sổ cái thù lao của cố vấn.", HttpStatusCode.Forbidden);

        if (claim.Role == Role.Mentor)
        {
            if (mentorUserId.HasValue && mentorUserId.Value != claim.Id)
                return ApiResponse<List<MentorEarningEntryResponse>>.Fail("Cố vấn chỉ được xem sổ cái thù lao của chính mình.", HttpStatusCode.Forbidden);
            mentorUserId = claim.Id;
        }
        else if (claim.Role != Role.Admin)
        {
            return ApiResponse<List<MentorEarningEntryResponse>>.Fail("Truy cập bị từ chối.", HttpStatusCode.Forbidden);
        }

        var query = _context.MentorEarningEntries
            .Include(e => e.CertificateLevel)
            .Include(e => e.MentorBooking)
            .AsQueryable();

        if (mentorUserId.HasValue)
            query = query.Where(e => e.MentorUserId == mentorUserId.Value);

        var rows = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorEarningEntryResponse>>.Success(
            rows.Select(e => MapEarning(e, e.CertificateLevel?.Code)).ToList());
    }

    // =========================================================================
    // 4. MONTHLY PAYROLL & ADJUSTMENTS
    // =========================================================================

    public async Task<ApiResponse<List<MentorPayrollPeriodResponse>>> GenerateMonthlyPayrollAsync(
        GenerateMonthlyPayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<List<MentorPayrollPeriodResponse>>.Fail("Chỉ Quản trị viên mới có quyền tổng hợp bảng lương tháng.", HttpStatusCode.Forbidden);

        if (request.Year < 2020 || request.Year > 2100 || request.Month < 1 || request.Month > 12)
            return ApiResponse<List<MentorPayrollPeriodResponse>>.Fail("Kỳ lương tháng/năm không hợp lệ.");

        return await ExecuteInIsolationAsync(async ct =>
        {
            var nowUtc = DateTime.UtcNow;

            var eligibleEarningsQuery = _context.MentorEarningEntries
                .Include(e => e.CertificateLevel)
                .Where(e => e.Status == MentorEarningStatus.Eligible && e.PayrollPeriodId == null);

            if (request.MentorUserId.HasValue)
                eligibleEarningsQuery = eligibleEarningsQuery.Where(e => e.MentorUserId == request.MentorUserId.Value);

            var eligibleEarnings = await eligibleEarningsQuery
                .OrderBy(e => e.Id)
                .ToListAsync(ct);

            var mentorIds = eligibleEarnings.Select(e => e.MentorUserId).Distinct().ToList();
            if (request.MentorUserId.HasValue && !mentorIds.Contains(request.MentorUserId.Value))
            {
                mentorIds.Add(request.MentorUserId.Value);
            }

            var results = new List<MentorPayrollPeriodResponse>();

            foreach (var mentorId in mentorIds)
            {
                var payroll = await _context.MentorPayrollPeriods
                    .Include(p => p.MentorUser)
                    .Include(p => p.Lines)
                        .ThenInclude(l => l.EarningEntry)
                    .Include(p => p.Adjustments)
                    .FirstOrDefaultAsync(p => p.MentorUserId == mentorId
                        && p.Year == request.Year
                        && p.Month == request.Month
                        && p.SequenceNumber == 1, ct);

                if (payroll is not null && payroll.Status is MentorPayrollStatus.Approved or MentorPayrollStatus.Paid or MentorPayrollStatus.Closed)
                {
                    if (request.MentorUserId.HasValue)
                    {
                        return ApiResponse<List<MentorPayrollPeriodResponse>>.Fail(
                            "Kỳ bảng lương này đã được phê duyệt hoặc chi trả, không thể tổng hợp lại.",
                            HttpStatusCode.Conflict);
                    }
                    var existingOb = await _context.MentorPayoutObligations
                        .FirstOrDefaultAsync(o => o.PayrollPeriodId == payroll.Id, ct);
                    results.Add(MapPayrollPeriod(payroll, existingOb?.Id));
                    continue;
                }

                if (payroll is null)
                {
                    payroll = new MentorPayrollPeriod
                    {
                        MentorUserId = mentorId,
                        Year = request.Year,
                        Month = request.Month,
                        SequenceNumber = 1,
                        Status = MentorPayrollStatus.Review,
                        Currency = "VND",
                        PreparedByAdminId = claim.Id,
                        PreparedAtUtc = nowUtc,
                        ApprovalNotes = request.Notes?.Trim()
                    };
                    _context.MentorPayrollPeriods.Add(payroll);
                    await _context.SaveChangesAsync(ct);
                }

                var mentorEarnings = eligibleEarnings.Where(e => e.MentorUserId == mentorId).ToList();
                foreach (var earning in mentorEarnings)
                {
                    var line = new MentorPayrollLine
                    {
                        PayrollPeriodId = payroll.Id,
                        EarningEntryId = earning.Id,
                        MentorBookingId = earning.MentorBookingId,
                        FrozenHourlyRate = earning.FrozenHourlyRate,
                        FrozenLevelCoefficient = earning.FrozenLevelCoefficient,
                        ApprovedEligibleMinutes = earning.ApprovedEligibleMinutes,
                        LineAmount = earning.GrossAmount,
                        CreatedAtUtc = nowUtc
                    };
                    _context.MentorPayrollLines.Add(line);
                    await _context.SaveChangesAsync(ct);

                    earning.PayrollPeriodId = payroll.Id;
                    earning.Status = MentorEarningStatus.LockedInPayroll;
                    _context.MentorEarningEntries.Update(earning);
                }

                RecalculatePayrollTotals(payroll);
                await _context.SaveChangesAsync(ct);

                var reloaded = await LoadPayrollPeriodFullAsync(payroll.Id, ct);
                if (reloaded is not null)
                {
                    var ob = await _context.MentorPayoutObligations
                        .FirstOrDefaultAsync(o => o.PayrollPeriodId == reloaded.Id, ct);
                    results.Add(MapPayrollPeriod(reloaded, ob?.Id));
                }
            }

            return ApiResponse<List<MentorPayrollPeriodResponse>>.Success(
                results,
                HttpStatusCode.OK,
                "Tổng hợp bảng lương tháng cho cố vấn thành công.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayrollAdjustmentResponse>> AddPayrollAdjustmentAsync(
        long payrollPeriodId,
        CreatePayrollAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayrollAdjustmentResponse>.Fail("Chỉ Quản trị viên mới có quyền tạo bút toán điều chỉnh bảng lương.", HttpStatusCode.Forbidden);

        if (request.AmountVnd == 0)
            return ApiResponse<MentorPayrollAdjustmentResponse>.Fail("Số tiền điều chỉnh phải khác 0.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<MentorPayrollAdjustmentResponse>.Fail("Lý do điều chỉnh là bắt buộc.");

        return await ExecuteInIsolationAsync(async ct =>
        {
            var payroll = await LoadPayrollPeriodFullAsync(payrollPeriodId, ct);
            if (payroll is null)
                return ApiResponse<MentorPayrollAdjustmentResponse>.Fail("Không tìm thấy kỳ bảng lương.", HttpStatusCode.NotFound);

            if (payroll.Status is not (MentorPayrollStatus.Draft or MentorPayrollStatus.Review))
                return ApiResponse<MentorPayrollAdjustmentResponse>.Fail(
                    "Chỉ được thêm bút toán điều chỉnh khi bảng lương đang ở trạng thái Nháp (Draft) hoặc Chờ duyệt (Review).",
                    HttpStatusCode.Conflict);

            var opKey = !string.IsNullOrWhiteSpace(request.OperationKey)
                ? request.OperationKey.Trim()
                : $"adj:payroll:{payrollPeriodId}:{Guid.NewGuid():N}";

            var existingAdj = await _context.MentorPayrollAdjustments
                .FirstOrDefaultAsync(a => a.OperationKey == opKey, ct);
            if (existingAdj is not null)
            {
                return ApiResponse<MentorPayrollAdjustmentResponse>.Success(
                    MapAdjustment(existingAdj, payroll.MentorUserId),
                    message: "Bút toán điều chỉnh đã tồn tại (idempotent).");
            }

            var projectedGross = payroll.TotalEarningsAmount + payroll.TotalAdjustmentsAmount + request.AmountVnd;
            if (projectedGross < 0)
            {
                return ApiResponse<MentorPayrollAdjustmentResponse>.Fail(
                    "Tổng số tiền phải trả của kỳ bảng lương không được âm sau khi điều chỉnh.",
                    HttpStatusCode.BadRequest);
            }

            var nowUtc = DateTime.UtcNow;
            var adj = new MentorPayrollAdjustment
            {
                PayrollPeriodId = payroll.Id,
                RelatedBookingId = request.RelatedMentorBookingId,
                SignedAmount = request.AmountVnd,
                Reason = request.Reason.Trim(),
                OperationKey = opKey,
                ApprovedByAdminId = claim.Id,
                CreatedAtUtc = nowUtc
            };

            _context.MentorPayrollAdjustments.Add(adj);
            RecalculatePayrollTotals(payroll);

            await _context.SaveChangesAsync(ct);

            return ApiResponse<MentorPayrollAdjustmentResponse>.Success(
                MapAdjustment(adj, payroll.MentorUserId),
                HttpStatusCode.Created,
                "Thêm bút toán điều chỉnh bảng lương thành công.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayrollPeriodResponse>> ApproveMonthlyPayrollAsync(
        long payrollPeriodId,
        ApproveMonthlyPayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayrollPeriodResponse>.Fail("Chỉ Quản trị viên mới có quyền phê duyệt bảng lương tháng.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var payroll = await LoadPayrollPeriodFullAsync(payrollPeriodId, ct);
            if (payroll is null)
                return ApiResponse<MentorPayrollPeriodResponse>.Fail("Không tìm thấy kỳ bảng lương.", HttpStatusCode.NotFound);

            if (payroll.Status == MentorPayrollStatus.Paid)
                return ApiResponse<MentorPayrollPeriodResponse>.Fail("Bảng lương này đã được chi trả.", HttpStatusCode.Conflict);

            var nowUtc = DateTime.UtcNow;
            RecalculatePayrollTotals(payroll);

            if (payroll.GrossPayableAmount <= 0)
                return ApiResponse<MentorPayrollPeriodResponse>.Fail("Tổng tiền phải trả phải lớn hơn 0 để khởi tạo nghĩa vụ chi trả.", HttpStatusCode.BadRequest);

            payroll.Status = MentorPayrollStatus.Approved;
            payroll.ApprovedByAdminId = claim.Id;
            payroll.ApprovedAtUtc = nowUtc;
            if (!string.IsNullOrWhiteSpace(request.Notes))
                payroll.ApprovalNotes = request.Notes.Trim();
            _context.MentorPayrollPeriods.Update(payroll);

            var obligation = await _context.MentorPayoutObligations
                .FirstOrDefaultAsync(o => o.PayrollPeriodId == payroll.Id, ct);

            var currentBank = await _context.MentorBankAccounts
                .FirstOrDefaultAsync(b => b.MentorUserId == payroll.MentorUserId && b.IsCurrent, ct);

            if (obligation is null)
            {
                obligation = new MentorPayoutObligation
                {
                    PayrollPeriodId = payroll.Id,
                    MentorUserId = payroll.MentorUserId,
                    LockedBankAccountId = currentBank?.Id,
                    SelectedMethod = request.DefaultPayoutMethod ?? PayoutExecutionMethod.ManualBankTransfer,
                    Status = MentorPayoutObligationStatus.PendingExecution,
                    PayableAmount = payroll.GrossPayableAmount,
                    Currency = payroll.Currency,
                    ApprovedByAdminId = claim.Id,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                };
                _context.MentorPayoutObligations.Add(obligation);
            }
            else if (obligation.Status is MentorPayoutObligationStatus.PendingExecution or MentorPayoutObligationStatus.FailedRetryable)
            {
                obligation.PayableAmount = payroll.GrossPayableAmount;
                obligation.LockedBankAccountId = currentBank?.Id ?? obligation.LockedBankAccountId;
                if (request.DefaultPayoutMethod.HasValue)
                    obligation.SelectedMethod = request.DefaultPayoutMethod.Value;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
            }

            await _context.SaveChangesAsync(ct);

            var reloaded = await LoadPayrollPeriodFullAsync(payroll.Id, ct);
            return ApiResponse<MentorPayrollPeriodResponse>.Success(
                MapPayrollPeriod(reloaded!, obligation.Id),
                HttpStatusCode.OK,
                "Phê duyệt bảng lương tháng và khởi tạo nghĩa vụ chi trả thành công.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayrollPeriodResponse>> GetPayrollPeriodAsync(
        long payrollPeriodId,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<MentorPayrollPeriodResponse>.Fail("Học viên không có quyền xem bảng lương của cố vấn.", HttpStatusCode.Forbidden);

        var payroll = await LoadPayrollPeriodFullAsync(payrollPeriodId, cancellationToken);
        if (payroll is null)
            return ApiResponse<MentorPayrollPeriodResponse>.Fail("Không tìm thấy kỳ bảng lương.", HttpStatusCode.NotFound);

        if (claim.Role == Role.Mentor && payroll.MentorUserId != claim.Id)
            return ApiResponse<MentorPayrollPeriodResponse>.Fail("Cố vấn chỉ được xem bảng lương của chính mình.", HttpStatusCode.Forbidden);

        var ob = await _context.MentorPayoutObligations
            .FirstOrDefaultAsync(o => o.PayrollPeriodId == payroll.Id, cancellationToken);

        return ApiResponse<MentorPayrollPeriodResponse>.Success(MapPayrollPeriod(payroll, ob?.Id));
    }

    public async Task<ApiResponse<List<MentorPayrollPeriodResponse>>> GetPayrollPeriodsAsync(
        int? mentorUserId = null,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<List<MentorPayrollPeriodResponse>>.Fail("Học viên không có quyền xem bảng lương của cố vấn.", HttpStatusCode.Forbidden);

        if (claim.Role == Role.Mentor)
        {
            if (mentorUserId.HasValue && mentorUserId.Value != claim.Id)
                return ApiResponse<List<MentorPayrollPeriodResponse>>.Fail("Cố vấn chỉ được xem bảng lương của chính mình.", HttpStatusCode.Forbidden);
            mentorUserId = claim.Id;
        }

        var query = _context.MentorPayrollPeriods
            .Include(p => p.MentorUser)
            .Include(p => p.Lines)
                .ThenInclude(l => l.EarningEntry)
            .Include(p => p.Adjustments)
            .AsQueryable();

        if (mentorUserId.HasValue)
            query = query.Where(p => p.MentorUserId == mentorUserId.Value);
        if (year.HasValue)
            query = query.Where(p => p.Year == year.Value);
        if (month.HasValue)
            query = query.Where(p => p.Month == month.Value);

        var rows = await query
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .ThenBy(p => p.MentorUserId)
            .ToListAsync(cancellationToken);

        var payrollIds = rows.Select(r => r.Id).ToList();
        var obligationsByPayroll = await _context.MentorPayoutObligations
            .Where(o => payrollIds.Contains(o.PayrollPeriodId))
            .ToDictionaryAsync(o => o.PayrollPeriodId, o => o.Id, cancellationToken);

        return ApiResponse<List<MentorPayrollPeriodResponse>>.Success(
            rows.Select(p => MapPayrollPeriod(p, obligationsByPayroll.TryGetValue(p.Id, out var obId) ? obId : null)).ToList());
    }

    // =========================================================================
    // 5. MENTOR BANK ACCOUNT PROTECTION & ORGANIZATION PAYOUT ACCOUNTS
    // =========================================================================

    public async Task<ApiResponse<MentorBankAccountResponse>> UpsertMyBankAccountAsync(
        UpsertMentorBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
            return ApiResponse<MentorBankAccountResponse>.Fail("Chỉ Cố vấn mới có thể đăng ký/cập nhật tài khoản ngân hàng nhận thù lao của mình.", HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(request.BankBin) || string.IsNullOrWhiteSpace(request.BankShortName))
            return ApiResponse<MentorBankAccountResponse>.Fail("Mã BIN và tên viết tắt ngân hàng là bắt buộc.");

        var cleanAccountNo = NormalizeAccountNumber(request.AccountNumber);
        if (cleanAccountNo.Length < 6 || cleanAccountNo.Length > 34 || !cleanAccountNo.All(char.IsLetterOrDigit))
            return ApiResponse<MentorBankAccountResponse>.Fail("Số tài khoản ngân hàng không hợp lệ (từ 6 đến 34 ký tự số/chữ).");

        if (string.IsNullOrWhiteSpace(request.AccountHolderName) || request.AccountHolderName.Trim().Length < 2)
            return ApiResponse<MentorBankAccountResponse>.Fail("Tên chủ tài khoản ngân hàng là bắt buộc.");

        return await ExecuteInIsolationAsync(async ct =>
        {
            var nowUtc = DateTime.UtcNow;
            var existingAccounts = await _context.MentorBankAccounts
                .Where(b => b.MentorUserId == claim.Id)
                .OrderByDescending(b => b.AccountVersion)
                .ToListAsync(ct);

            var nextVersion = (existingAccounts.FirstOrDefault()?.AccountVersion ?? 0) + 1;

            foreach (var current in existingAccounts.Where(b => b.IsCurrent))
            {
                current.IsCurrent = false;
                current.VerificationStatus = MentorBankVerificationStatus.Superseded;
                current.EffectiveUntilUtc = nowUtc;
                _context.MentorBankAccounts.Update(current);
            }

            if (existingAccounts.Any(b => b.IsCurrent))
            {
                await _context.SaveChangesAsync(ct);
            }

            var masked = MaskAccountNumber(cleanAccountNo);
            var encrypted = EncryptAccountNumber(cleanAccountNo);
            var hash = HashAccountNumber(request.BankBin.Trim(), cleanAccountNo);

            var bankAccount = new MentorBankAccount
            {
                MentorUserId = claim.Id,
                AccountVersion = nextVersion,
                BankCode = request.BankBin.Trim(),
                BankName = request.BankShortName.Trim(),
                MaskedAccountNumber = masked,
                EncryptedAccountNumber = encrypted,
                AccountNumberHash = hash,
                AccountHolderName = request.AccountHolderName.Trim().ToUpperInvariant(),
                VerificationStatus = MentorBankVerificationStatus.PendingVerification,
                IsCurrent = true,
                EffectiveFromUtc = nowUtc,
                CreatedAtUtc = nowUtc
            };

            _context.MentorBankAccounts.Add(bankAccount);
            await _context.SaveChangesAsync(ct);

            // Nếu cố vấn có nghĩa vụ chi trả chưa thực thi, trỏ sang phiên bản tài khoản mới (đang chờ xác minh) để tạm chặn chi trả cho tới khi Admin duyệt lại
            var pendingObligations = await _context.MentorPayoutObligations
                .Where(o => o.MentorUserId == claim.Id
                    && (o.Status == MentorPayoutObligationStatus.PendingExecution || o.Status == MentorPayoutObligationStatus.FailedRetryable))
                .ToListAsync(ct);

            foreach (var ob in pendingObligations)
            {
                ob.LockedBankAccountId = bankAccount.Id;
                ob.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(ob);
            }

            if (pendingObligations.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
            }

            return ApiResponse<MentorBankAccountResponse>.Success(
                MapBankAccount(bankAccount),
                HttpStatusCode.Created,
                "Cập nhật tài khoản ngân hàng thành công. Tài khoản đang chờ Quản trị viên xác minh trước khi chi trả.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<List<MentorBankAccountResponse>>> GetMyBankAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
            return ApiResponse<List<MentorBankAccountResponse>>.Fail("Chỉ Cố vấn mới được xem tài khoản ngân hàng nhận thù lao của mình.", HttpStatusCode.Forbidden);

        var rows = await _context.MentorBankAccounts
            .Where(b => b.MentorUserId == claim.Id)
            .OrderByDescending(b => b.AccountVersion)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorBankAccountResponse>>.Success(rows.Select(MapBankAccount).ToList());
    }

    public async Task<ApiResponse<MentorBankAccountResponse>> VerifyMentorBankAccountAsync(
        long bankAccountId,
        VerifyMentorBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorBankAccountResponse>.Fail("Chỉ Quản trị viên mới có quyền xác minh tài khoản ngân hàng của cố vấn.", HttpStatusCode.Forbidden);

        var bank = await _context.MentorBankAccounts
            .FirstOrDefaultAsync(b => b.Id == bankAccountId, cancellationToken);
        if (bank is null)
            return ApiResponse<MentorBankAccountResponse>.Fail("Không tìm thấy thông tin tài khoản ngân hàng.", HttpStatusCode.NotFound);

        if (!bank.IsCurrent && request.Approve)
            return ApiResponse<MentorBankAccountResponse>.Fail("Không thể xác minh phiên bản tài khoản ngân hàng cũ đã bị thay thế.", HttpStatusCode.Conflict);

        var nowUtc = DateTime.UtcNow;
        bank.VerificationStatus = request.Approve
            ? MentorBankVerificationStatus.Verified
            : MentorBankVerificationStatus.Rejected;
        bank.VerifiedByAdminId = claim.Id;
        bank.VerifiedAtUtc = nowUtc;
        bank.VerificationNotes = request.Notes?.Trim() ?? request.VerificationMethod?.Trim();

        _context.MentorBankAccounts.Update(bank);

        if (request.Approve)
        {
            var pendingObligations = await _context.MentorPayoutObligations
                .Where(o => o.MentorUserId == bank.MentorUserId
                    && (o.Status == MentorPayoutObligationStatus.PendingExecution || o.Status == MentorPayoutObligationStatus.FailedRetryable))
                .ToListAsync(cancellationToken);

            foreach (var ob in pendingObligations)
            {
                ob.LockedBankAccountId = bank.Id;
                ob.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(ob);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<MentorBankAccountResponse>.Success(
            MapBankAccount(bank),
            HttpStatusCode.OK,
            request.Approve ? "Đã xác minh tài khoản ngân hàng của cố vấn." : "Đã từ chối tài khoản ngân hàng của cố vấn.");
    }

    public async Task<ApiResponse<List<MentorBankAccountResponse>>> GetMentorBankAccountsForAdminAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<List<MentorBankAccountResponse>>.Fail("Chỉ Quản trị viên mới có quyền truy cập danh sách tài khoản ngân hàng cố vấn.", HttpStatusCode.Forbidden);

        var query = _context.MentorBankAccounts.AsQueryable();
        if (mentorUserId.HasValue)
            query = query.Where(b => b.MentorUserId == mentorUserId.Value);

        var rows = await query
            .OrderBy(b => b.MentorUserId)
            .ThenByDescending(b => b.AccountVersion)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorBankAccountResponse>>.Success(rows.Select(MapBankAccount).ToList());
    }

    public async Task<ApiResponse<OrganizationPayoutAccountResponse>> ConfigureOrganizationPayoutAccountAsync(
        ConfigureOrganizationPayoutAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<OrganizationPayoutAccountResponse>.Fail("Chỉ Quản trị viên mới có quyền cấu hình tài khoản nguồn chi trả.", HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.BankBin) ||
            string.IsNullOrWhiteSpace(request.BankShortName) ||
            string.IsNullOrWhiteSpace(request.MaskedAccountNumber) ||
            string.IsNullOrWhiteSpace(request.AccountHolderName))
        {
            return ApiResponse<OrganizationPayoutAccountResponse>.Fail("Vui lòng nhập đầy đủ thông tin tài khoản nguồn chi trả của tổ chức.");
        }

        var nowUtc = DateTime.UtcNow;
        OrganizationPayoutAccount account;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var found = await _context.OrganizationPayoutAccounts
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken);
            if (found is null)
                return ApiResponse<OrganizationPayoutAccountResponse>.Fail("Không tìm thấy tài khoản nguồn chi trả.", HttpStatusCode.NotFound);
            account = found;
        }
        else
        {
            account = new OrganizationPayoutAccount
            {
                CreatedAtUtc = nowUtc
            };
            _context.OrganizationPayoutAccounts.Add(account);
        }

        account.ChannelCode = !string.IsNullOrWhiteSpace(request.PayOsPayoutChannelCode)
            ? request.PayOsPayoutChannelCode.Trim()
            : request.DisplayName.Trim();
        account.ProviderType = request.SupportsPayOsPayout
            ? PayoutExecutionMethod.PayOsPayout
            : PayoutExecutionMethod.ManualBankTransfer;
        account.BankCode = request.BankBin.Trim();
        account.BankName = request.BankShortName.Trim();
        account.MaskedAccountNumber = MaskAccountNumber(request.MaskedAccountNumber.Trim());
        account.AccountHolderName = request.AccountHolderName.Trim().ToUpperInvariant();
        account.ProviderAccountReference = request.DisplayName.Trim();
        account.IsActive = request.IsActive;
        account.IsVerified = true;
        account.ConfiguredByAdminId = claim.Id;
        account.VerifiedAtUtc = nowUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<OrganizationPayoutAccountResponse>.Success(
            MapOrganizationAccount(account),
            HttpStatusCode.OK,
            "Lưu cấu hình tài khoản nguồn chi trả thành công.");
    }

    public async Task<ApiResponse<List<OrganizationPayoutAccountResponse>>> GetOrganizationPayoutAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<List<OrganizationPayoutAccountResponse>>.Fail("Chỉ Quản trị viên mới được xem tài khoản nguồn chi trả.", HttpStatusCode.Forbidden);

        var rows = await _context.OrganizationPayoutAccounts
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<OrganizationPayoutAccountResponse>>.Success(rows.Select(MapOrganizationAccount).ToList());
    }

    // =========================================================================
    // 6. DUAL PAYOUT (MANUAL BANK TRANSFER & PAYOS PAYOUT) & RECONCILIATION
    // =========================================================================

    public async Task<ApiResponse<MentorPayoutObligationResponse>> SelectPayoutMethodAsync(
        long obligationId,
        SelectPayoutMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Chỉ Quản trị viên mới có quyền chọn phương thức chi trả.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligation = await LoadObligationFullAsync(obligationId, ct);
            if (obligation is null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

            var switchError = ValidateObligationCanSelectOrSwitchMethod(obligation);
            if (switchError is not null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail(switchError, HttpStatusCode.Conflict);

            var verifiedBank = await GetVerifiedCurrentBankAccountAsync(obligation.MentorUserId, ct);
            if (verifiedBank is null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Cố vấn chưa có tài khoản ngân hàng hiện hành được Quản trị viên xác minh (Verified).",
                    HttpStatusCode.Conflict);
            }

            obligation.LockedBankAccountId = verifiedBank.Id;
            obligation.LockedBankAccount = verifiedBank;
            obligation.SelectedMethod = request.ExecutionMethod;
            obligation.OrganizationPayoutAccountId = request.OrganizationPayoutAccountId ?? obligation.OrganizationPayoutAccountId;
            obligation.Status = MentorPayoutObligationStatus.PendingExecution;
            obligation.UpdatedAtUtc = DateTime.UtcNow;

            _context.MentorPayoutObligations.Update(obligation);
            await _context.SaveChangesAsync(ct);

            return ApiResponse<MentorPayoutObligationResponse>.Success(
                MapObligation(obligation),
                HttpStatusCode.OK,
                $"Đã chọn phương thức chi trả {request.ExecutionMethod} cho nghĩa vụ #{obligation.Id}.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayoutObligationResponse>> InitiateManualPayoutAsync(
        long obligationId,
        InitiateManualPayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Chỉ Quản trị viên mới có quyền khởi tạo chuyển khoản thủ công.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligation = await LoadObligationFullAsync(obligationId, ct);
            if (obligation is null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

            var idempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey.Trim()
                : $"manual:init:obligation:{obligation.Id}:attempt:{obligation.Attempts.Count + 1}";

            var existingAttempt = obligation.Attempts.FirstOrDefault(a => a.IdempotencyKey == idempotencyKey);
            if (existingAttempt is not null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Success(
                    MapObligation(obligation),
                    message: "Lệnh khởi tạo chuyển khoản thủ công đã được ghi nhận trước đó (idempotent).");
            }

            var precheckError = ValidateObligationBeforeExecution(obligation);
            if (precheckError is not null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail(precheckError, HttpStatusCode.Conflict);

            var verifiedBank = await GetVerifiedCurrentBankAccountAsync(obligation.MentorUserId, ct);
            if (verifiedBank is null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Cố vấn chưa có tài khoản ngân hàng hiện hành được Quản trị viên xác minh (Verified). Không thể khởi tạo chi trả.",
                    HttpStatusCode.Conflict);
            }

            if (request.OrganizationPayoutAccountId.HasValue)
            {
                var orgAcc = await _context.OrganizationPayoutAccounts
                    .FirstOrDefaultAsync(x => x.IsActive && x.Id == request.OrganizationPayoutAccountId.Value, ct);
                if (orgAcc is null)
                    return ApiResponse<MentorPayoutObligationResponse>.Fail("Tài khoản nguồn chi trả không hợp lệ.", HttpStatusCode.BadRequest);
                obligation.OrganizationPayoutAccountId = orgAcc.Id;
            }

            var nowUtc = DateTime.UtcNow;
            var attemptNumber = (obligation.Attempts.MaxBy(a => a.AttemptNumber)?.AttemptNumber ?? 0) + 1;
            var attempt = new MentorPayoutAttempt
            {
                PayoutObligationId = obligation.Id,
                AttemptNumber = attemptNumber,
                ExecutionMethod = PayoutExecutionMethod.ManualBankTransfer,
                InternalStatus = MentorPayoutAttemptStatus.Initiated,
                Amount = obligation.PayableAmount,
                Currency = obligation.Currency,
                IdempotencyKey = idempotencyKey,
                LockedBankAccountId = verifiedBank.Id,
                EvidenceNotes = request.Notes?.Trim(),
                InitiatedByAdminId = claim.Id,
                CreatedAtUtc = nowUtc
            };

            _context.MentorPayoutAttempts.Add(attempt);
            await _context.SaveChangesAsync(ct);

            obligation.LockedBankAccountId = verifiedBank.Id;
            obligation.SelectedMethod = PayoutExecutionMethod.ManualBankTransfer;
            obligation.Status = MentorPayoutObligationStatus.Processing;
            obligation.ActiveAttemptId = attempt.Id;
            obligation.UpdatedAtUtc = nowUtc;
            _context.MentorPayoutObligations.Update(obligation);
            await _context.SaveChangesAsync(ct);

            var reloaded = await LoadObligationFullAsync(obligation.Id, ct);
            return ApiResponse<MentorPayoutObligationResponse>.Success(
                MapObligation(reloaded!),
                HttpStatusCode.OK,
                "Khởi tạo lệnh chuyển khoản thủ công thành công. Đang chờ xác nhận mã giao dịch ngân hàng và chứng từ.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayoutObligationResponse>> ConfirmManualPayoutAsync(
        long obligationId,
        ConfirmManualPayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Chỉ Quản trị viên mới có quyền xác nhận hoàn tất chuyển khoản thủ công.", HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(request.TransferReference))
            return ApiResponse<MentorPayoutObligationResponse>.Fail(
                "Mã tham chiếu giao dịch ngân hàng (TransferReference) là bắt buộc để xác nhận chi trả thủ công.",
                HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(request.EvidenceStorageKey))
            return ApiResponse<MentorPayoutObligationResponse>.Fail(
                "Chứng từ chuyển khoản (EvidenceStorageKey) là bắt buộc; không được đánh dấu Đã thanh toán (Paid) khi thiếu chứng từ.",
                HttpStatusCode.BadRequest);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligation = await LoadObligationFullAsync(obligationId, ct);
            if (obligation is null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

            var transferRef = request.TransferReference.Trim();
            var evidenceKey = request.EvidenceStorageKey.Trim();

            if (obligation.Status == MentorPayoutObligationStatus.Paid)
            {
                var existingPaidAttempt = obligation.Attempts.FirstOrDefault(a => a.Id == obligation.SettledAttemptId);
                if (existingPaidAttempt is not null &&
                    existingPaidAttempt.ExecutionMethod == PayoutExecutionMethod.ManualBankTransfer &&
                    string.Equals(existingPaidAttempt.TransferReference, transferRef, StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<MentorPayoutObligationResponse>.Success(
                        MapObligation(obligation),
                        message: "Giao dịch chuyển khoản thủ công này đã được xác nhận hoàn tất trước đó (idempotent).");
                }

                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Nghĩa vụ chi trả này đã được thanh toán hoàn tất (Paid), không thể thanh toán lặp lần hai.",
                    HttpStatusCode.Conflict);
            }

            if (obligation.Status == MentorPayoutObligationStatus.UnknownPendingReconciliation)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Nghĩa vụ chi trả đang ở trạng thái chờ đối soát (UnknownPendingReconciliation); không thể xác nhận chuyển khoản thủ công chồng chéo.",
                    HttpStatusCode.Conflict);
            }

            if (obligation.Attempts.Any(a => a.InternalStatus == MentorPayoutAttemptStatus.Succeeded))
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Nghĩa vụ chi trả đã có một lần chi trả thành công trước đó.",
                    HttpStatusCode.Conflict);
            }

            if (obligation.Attempts.Any(a => a.ExecutionMethod == PayoutExecutionMethod.PayOsPayout &&
                a.InternalStatus is MentorPayoutAttemptStatus.Initiated or MentorPayoutAttemptStatus.Processing or MentorPayoutAttemptStatus.UnknownPendingReconciliation))
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Đang có giao dịch payOS Payout chưa kết thúc cho nghĩa vụ này; không được phép xác nhận thủ công chồng chéo.",
                    HttpStatusCode.Conflict);
            }

            var duplicateRefExists = await _context.MentorPayoutAttempts
                .AnyAsync(a => a.InternalStatus == MentorPayoutAttemptStatus.Succeeded
                    && a.TransferReference == transferRef
                    && a.PayoutObligationId != obligation.Id, ct);
            if (duplicateRefExists)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Mã tham chiếu giao dịch ngân hàng (TransferReference) đã được sử dụng cho một nghĩa vụ chi trả khác.",
                    HttpStatusCode.Conflict);
            }

            var verifiedBank = await GetVerifiedCurrentBankAccountAsync(obligation.MentorUserId, ct);
            if (verifiedBank is null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Cố vấn chưa có tài khoản ngân hàng hiện hành được Quản trị viên xác minh (Verified).",
                    HttpStatusCode.Conflict);
            }

            var nowUtc = DateTime.UtcNow;
            var confirmedAt = request.TransferredAtUtc.HasValue ? NormalizeUtc(request.TransferredAtUtc.Value) : nowUtc;
            obligation.LockedBankAccountId = verifiedBank.Id;

            var activeManualAttempt = obligation.Attempts
                .FirstOrDefault(a => a.ExecutionMethod == PayoutExecutionMethod.ManualBankTransfer
                    && a.InternalStatus is MentorPayoutAttemptStatus.Initiated or MentorPayoutAttemptStatus.OnHoldMissingEvidence);

            if (activeManualAttempt is null)
            {
                var attemptNumber = (obligation.Attempts.MaxBy(a => a.AttemptNumber)?.AttemptNumber ?? 0) + 1;
                var idempKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? request.IdempotencyKey.Trim()
                    : $"manual:confirm:obligation:{obligation.Id}:attempt:{attemptNumber}";

                activeManualAttempt = new MentorPayoutAttempt
                {
                    PayoutObligationId = obligation.Id,
                    AttemptNumber = attemptNumber,
                    ExecutionMethod = PayoutExecutionMethod.ManualBankTransfer,
                    InternalStatus = MentorPayoutAttemptStatus.Succeeded,
                    Amount = obligation.PayableAmount,
                    Currency = obligation.Currency,
                    IdempotencyKey = idempKey,
                    LockedBankAccountId = verifiedBank.Id,
                    TransferReference = transferRef,
                    EvidenceStorageKey = evidenceKey,
                    EvidenceNotes = request.Notes?.Trim(),
                    ProviderStatus = "MANUAL_CONFIRMED",
                    InitiatedByAdminId = claim.Id,
                    ConfirmedByAdminId = claim.Id,
                    CreatedAtUtc = nowUtc,
                    CompletedAtUtc = confirmedAt
                };
                _context.MentorPayoutAttempts.Add(activeManualAttempt);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                activeManualAttempt.InternalStatus = MentorPayoutAttemptStatus.Succeeded;
                activeManualAttempt.LockedBankAccountId = verifiedBank.Id;
                activeManualAttempt.TransferReference = transferRef;
                activeManualAttempt.EvidenceStorageKey = evidenceKey;
                activeManualAttempt.ProviderStatus = "MANUAL_CONFIRMED";
                activeManualAttempt.ConfirmedByAdminId = claim.Id;
                activeManualAttempt.CompletedAtUtc = confirmedAt;
                if (!string.IsNullOrWhiteSpace(request.Notes))
                    activeManualAttempt.EvidenceNotes = request.Notes.Trim();
                _context.MentorPayoutAttempts.Update(activeManualAttempt);
            }

            await MarkObligationAndPayrollPaidAsync(obligation, activeManualAttempt.Id, transferRef, nowUtc, ct);
            await _context.SaveChangesAsync(ct);

            var reloaded = await LoadObligationFullAsync(obligation.Id, ct);
            return ApiResponse<MentorPayoutObligationResponse>.Success(
                MapObligation(reloaded!),
                HttpStatusCode.OK,
                "Xác nhận chuyển khoản thủ công và tất toán bảng lương cố vấn thành công.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayoutObligationResponse>> ExecutePayOsPayoutAsync(
        long obligationId,
        ExecutePayOsPayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Chỉ Quản trị viên mới có quyền thực thi chi trả qua payOS Payout.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligation = await LoadObligationFullAsync(obligationId, ct);
            if (obligation is null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var replayAttempt = obligation.Attempts.FirstOrDefault(a => a.IdempotencyKey == request.IdempotencyKey.Trim());
                if (replayAttempt is not null)
                {
                    return ApiResponse<MentorPayoutObligationResponse>.Success(
                        MapObligation(obligation),
                        message: "Lệnh chi trả payOS đã được xử lý trước đó theo IdempotencyKey (idempotent).");
                }
            }

            var precheckError = ValidateObligationBeforeExecution(obligation);
            if (precheckError is not null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail(precheckError, HttpStatusCode.Conflict);

            var verifiedBank = await GetVerifiedCurrentBankAccountAsync(obligation.MentorUserId, ct);
            if (verifiedBank is null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Cố vấn chưa có tài khoản ngân hàng hiện hành được Quản trị viên xác minh (Verified). Không thể chi trả qua payOS.",
                    HttpStatusCode.Conflict);
            }

            var nowUtc = DateTime.UtcNow;
            obligation.LockedBankAccountId = verifiedBank.Id;
            obligation.SelectedMethod = PayoutExecutionMethod.PayOsPayout;
            obligation.OrganizationPayoutAccountId = request.OrganizationPayoutAccountId ?? obligation.OrganizationPayoutAccountId;

            var attemptNumber = (obligation.Attempts.MaxBy(a => a.AttemptNumber)?.AttemptNumber ?? 0) + 1;
            var referenceId = $"rikipath_po_{obligation.Id}_{attemptNumber}";
            var idempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey.Trim()
                : $"payos:obligation:{obligation.Id}:attempt:{attemptNumber}";

            var decryptedAccountNumber = DecryptAccountNumber(verifiedBank.EncryptedAccountNumber);
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Thu lao co van #{obligation.MentorUserId} ky {obligation.PayrollPeriod?.Month:D2}/{obligation.PayrollPeriod?.Year}"
                : request.Description.Trim();

            var amountLong = (long)decimal.Round(obligation.PayableAmount, 0, MidpointRounding.AwayFromZero);

            var providerResult = await _payOsPayoutClient.CreateSinglePayoutAsync(new PayOsSinglePayoutCommand(
                ReferenceId: referenceId,
                AmountVnd: amountLong,
                Description: description,
                ToBin: verifiedBank.BankCode,
                ToAccountNumber: decryptedAccountNumber,
                Categories: request.Categories ?? ["salary"],
                IdempotencyKey: idempotencyKey,
                QaSimulatedOutcome: request.QaSimulatedOutcome), ct);

            var attemptStatus = ResolveAttemptStatusFromProvider(providerResult);

            var attempt = new MentorPayoutAttempt
            {
                PayoutObligationId = obligation.Id,
                AttemptNumber = attemptNumber,
                ExecutionMethod = PayoutExecutionMethod.PayOsPayout,
                InternalStatus = attemptStatus,
                Amount = obligation.PayableAmount,
                Currency = obligation.Currency,
                IdempotencyKey = idempotencyKey,
                LockedBankAccountId = verifiedBank.Id,
                ProviderPayoutId = providerResult.ProviderPayoutId ?? providerResult.ReferenceId,
                BatchId = providerResult.ProviderBatchId,
                ProviderStatus = providerResult.ApprovalState,
                TransferReference = providerResult.ProviderTransactionId,
                ErrorCode = providerResult.ErrorCode,
                ErrorMessage = providerResult.ErrorMessage,
                EvidenceNotes = providerResult.RawPayloadSummaryJson,
                InitiatedByAdminId = claim.Id,
                ConfirmedByAdminId = attemptStatus == MentorPayoutAttemptStatus.Succeeded ? claim.Id : null,
                CreatedAtUtc = nowUtc,
                CompletedAtUtc = attemptStatus is MentorPayoutAttemptStatus.Succeeded or MentorPayoutAttemptStatus.Failed ? nowUtc : null
            };

            _context.MentorPayoutAttempts.Add(attempt);
            await _context.SaveChangesAsync(ct);

            var settledRef = providerResult.ProviderTransactionId ?? providerResult.ProviderPayoutId ?? referenceId;
            await ApplyAttemptOutcomeToObligationAsync(obligation, attempt, settledRef, nowUtc, ct);
            await _context.SaveChangesAsync(ct);

            var reloaded = await LoadObligationFullAsync(obligation.Id, ct);
            var msg = attemptStatus switch
            {
                MentorPayoutAttemptStatus.Succeeded => "Chi trả qua payOS Payout hoàn tất thành công (Paid).",
                MentorPayoutAttemptStatus.Processing => "Lệnh chi trả payOS Payout đã được tiếp nhận và đang xử lý (PROCESSING - chưa đánh dấu Paid).",
                MentorPayoutAttemptStatus.UnknownPendingReconciliation => "Chưa xác định được kết quả phản hồi từ payOS Payout (UnknownPendingReconciliation); bắt buộc đối soát trước khi thử lại.",
                _ => $"Chi trả qua payOS Payout thất bại: {providerResult.ErrorMessage}"
            };

            return ApiResponse<MentorPayoutObligationResponse>.Success(MapObligation(reloaded!), HttpStatusCode.OK, msg);
        }, cancellationToken);
    }

    public async Task<ApiResponse<PayOsBatchPayoutExecutionResponse>> ExecutePayOsBatchPayoutAsync(
        ExecutePayOsBatchPayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<PayOsBatchPayoutExecutionResponse>.Fail("Chỉ Quản trị viên mới có quyền thực thi chi trả hàng loạt qua payOS.", HttpStatusCode.Forbidden);

        var distinctIds = request.ObligationIds?.Distinct().ToList() ?? [];
        if (distinctIds.Count == 0)
            return ApiResponse<PayOsBatchPayoutExecutionResponse>.Fail("Danh sách nghĩa vụ chi trả hàng loạt không được để trống.");

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligations = new List<MentorPayoutObligation>();
            var verifiedBanks = new Dictionary<long, MentorBankAccount>();
            var itemCommands = new List<PayOsBatchPayoutItemCommand>();
            var refToObligation = new Dictionary<string, MentorPayoutObligation>(StringComparer.OrdinalIgnoreCase);
            var outcomesByRef = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obId in distinctIds)
            {
                var ob = await LoadObligationFullAsync(obId, ct);
                if (ob is null)
                    return ApiResponse<PayOsBatchPayoutExecutionResponse>.Fail($"Không tìm thấy nghĩa vụ chi trả #{obId}.", HttpStatusCode.NotFound);

                var err = ValidateObligationBeforeExecution(ob);
                if (err is not null)
                    return ApiResponse<PayOsBatchPayoutExecutionResponse>.Fail($"Nghĩa vụ #{obId}: {err}", HttpStatusCode.Conflict);

                var bank = await GetVerifiedCurrentBankAccountAsync(ob.MentorUserId, ct);
                if (bank is null)
                    return ApiResponse<PayOsBatchPayoutExecutionResponse>.Fail(
                        $"Nghĩa vụ #{obId}: Cố vấn #{ob.MentorUserId} chưa có tài khoản ngân hàng được xác minh.",
                        HttpStatusCode.Conflict);

                var nextAttemptNo = (ob.Attempts.MaxBy(a => a.AttemptNumber)?.AttemptNumber ?? 0) + 1;
                var itemRef = $"rikipath_batch_item_{ob.Id}_{nextAttemptNo}";
                var plainAccount = DecryptAccountNumber(bank.EncryptedAccountNumber);
                var amountLong = (long)decimal.Round(ob.PayableAmount, 0, MidpointRounding.AwayFromZero);

                obligations.Add(ob);
                verifiedBanks[ob.Id] = bank;
                refToObligation[itemRef] = ob;

                if (request.QaSimulatedOutcomesByObligation != null &&
                    request.QaSimulatedOutcomesByObligation.TryGetValue(ob.Id, out var simOutcome))
                {
                    outcomesByRef[itemRef] = simOutcome;
                }

                itemCommands.Add(new PayOsBatchPayoutItemCommand(
                    ReferenceId: itemRef,
                    AmountVnd: amountLong,
                    Description: request.Description ?? $"Thu lao co van #{ob.MentorUserId}",
                    ToBin: bank.BankCode,
                    ToAccountNumber: plainAccount));
            }

            var batchRefId = $"rikipath_batch_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            var batchIdempKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey.Trim()
                : $"payos:batch:{string.Join("_", distinctIds.OrderBy(x => x))}:{batchRefId}";

            var batchResult = await _payOsPayoutClient.CreateBatchPayoutAsync(new PayOsBatchPayoutCommand(
                BatchReferenceId: batchRefId,
                ValidateDestination: true,
                Categories: request.Categories ?? ["salary"],
                Payouts: itemCommands,
                IdempotencyKey: batchIdempKey,
                QaSimulatedOutcomesByReferenceId: outcomesByRef), ct);

            var nowUtc = DateTime.UtcNow;
            int succeeded = 0, processing = 0, failed = 0, unknown = 0;

            foreach (var itemResult in batchResult.Items)
            {
                if (!refToObligation.TryGetValue(itemResult.ReferenceId, out var ob))
                    continue;

                var bank = verifiedBanks[ob.Id];
                ob.LockedBankAccountId = bank.Id;
                ob.SelectedMethod = PayoutExecutionMethod.PayOsPayout;
                ob.OrganizationPayoutAccountId = request.OrganizationPayoutAccountId ?? ob.OrganizationPayoutAccountId;

                var attemptNumber = (ob.Attempts.MaxBy(a => a.AttemptNumber)?.AttemptNumber ?? 0) + 1;
                var attemptStatus = ResolveAttemptStatusFromProvider(itemResult);

                switch (attemptStatus)
                {
                    case MentorPayoutAttemptStatus.Succeeded: succeeded++; break;
                    case MentorPayoutAttemptStatus.Processing: processing++; break;
                    case MentorPayoutAttemptStatus.UnknownPendingReconciliation: unknown++; break;
                    default: failed++; break;
                }

                var attempt = new MentorPayoutAttempt
                {
                    PayoutObligationId = ob.Id,
                    AttemptNumber = attemptNumber,
                    ExecutionMethod = PayoutExecutionMethod.PayOsPayout,
                    InternalStatus = attemptStatus,
                    Amount = ob.PayableAmount,
                    Currency = ob.Currency,
                    IdempotencyKey = $"{batchIdempKey}:{ob.Id}",
                    LockedBankAccountId = bank.Id,
                    ProviderPayoutId = itemResult.ProviderPayoutId ?? itemResult.ReferenceId,
                    BatchId = batchResult.ProviderBatchId ?? batchRefId,
                    ProviderStatus = itemResult.ApprovalState,
                    TransferReference = itemResult.ProviderTransactionId,
                    ErrorCode = itemResult.ErrorCode,
                    ErrorMessage = itemResult.ErrorMessage,
                    EvidenceNotes = itemResult.RawPayloadSummaryJson,
                    InitiatedByAdminId = claim.Id,
                    ConfirmedByAdminId = attemptStatus == MentorPayoutAttemptStatus.Succeeded ? claim.Id : null,
                    CreatedAtUtc = nowUtc,
                    CompletedAtUtc = attemptStatus is MentorPayoutAttemptStatus.Succeeded or MentorPayoutAttemptStatus.Failed ? nowUtc : null
                };

                _context.MentorPayoutAttempts.Add(attempt);
                await _context.SaveChangesAsync(ct);

                var settledRef = itemResult.ProviderTransactionId ?? itemResult.ProviderPayoutId ?? itemResult.ReferenceId;
                await ApplyAttemptOutcomeToObligationAsync(ob, attempt, settledRef, nowUtc, ct);
                await _context.SaveChangesAsync(ct);
            }

            var updatedResponses = new List<MentorPayoutObligationResponse>();
            foreach (var obId in distinctIds)
            {
                var reloaded = await LoadObligationFullAsync(obId, ct);
                if (reloaded is not null)
                    updatedResponses.Add(MapObligation(reloaded));
            }

            return ApiResponse<PayOsBatchPayoutExecutionResponse>.Success(new PayOsBatchPayoutExecutionResponse
            {
                BatchReferenceId = batchRefId,
                ProviderBatchId = batchResult.ProviderBatchId,
                TotalObligations = distinctIds.Count,
                SucceededCount = succeeded,
                ProcessingCount = processing,
                FailedCount = failed,
                UnknownCount = unknown,
                Obligations = updatedResponses
            }, HttpStatusCode.OK, "Thực thi lô chi trả payOS Batch Payout hoàn tất.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayoutObligationResponse>> ReconcilePayoutObligationAsync(
        long obligationId,
        ReconcilePayoutObligationRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Chỉ Quản trị viên mới có quyền đối soát giao dịch chi trả.", HttpStatusCode.Forbidden);

        return await ExecuteInIsolationAsync(async ct =>
        {
            var obligation = await LoadObligationFullAsync(obligationId, ct);
            if (obligation is null)
                return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

            if (obligation.Status == MentorPayoutObligationStatus.Paid)
                return ApiResponse<MentorPayoutObligationResponse>.Success(MapObligation(obligation), message: "Nghĩa vụ chi trả đã hoàn tất (Paid).");

            var activeAttempt = obligation.Attempts
                .OrderByDescending(a => a.AttemptNumber)
                .FirstOrDefault(a => a.InternalStatus is MentorPayoutAttemptStatus.UnknownPendingReconciliation or MentorPayoutAttemptStatus.Processing or MentorPayoutAttemptStatus.Initiated);

            if (activeAttempt is null)
            {
                return ApiResponse<MentorPayoutObligationResponse>.Fail(
                    "Không có lần thử chi trả nào đang chờ đối soát hoặc đang xử lý.",
                    HttpStatusCode.Conflict);
            }

            var finalState = request.ProviderFinalState?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(finalState))
            {
                if (!string.IsNullOrWhiteSpace(activeAttempt.ProviderPayoutId))
                {
                    var remote = await _payOsPayoutClient.GetPayoutAsync(activeAttempt.ProviderPayoutId, ct);
                    finalState = remote.ApprovalState.ToUpperInvariant();
                }
                else
                {
                    finalState = "FAILED";
                }
            }

            var nowUtc = DateTime.UtcNow;

            if (finalState is "COMPLETED" or "SUCCEEDED" or "PAID")
            {
                var settledRef = request.ProviderTransactionId
                    ?? activeAttempt.TransferReference
                    ?? activeAttempt.ProviderPayoutId
                    ?? $"reconciled_{activeAttempt.Id}";

                activeAttempt.InternalStatus = MentorPayoutAttemptStatus.Succeeded;
                activeAttempt.ProviderStatus = "COMPLETED";
                activeAttempt.TransferReference = settledRef;
                activeAttempt.ConfirmedByAdminId = claim.Id;
                activeAttempt.CompletedAtUtc = nowUtc;
                activeAttempt.EvidenceNotes = request.Reason?.Trim() ?? "Đối soát xác nhận giao dịch đã thành công trên cổng thanh toán.";
                _context.MentorPayoutAttempts.Update(activeAttempt);

                await MarkObligationAndPayrollPaidAsync(obligation, activeAttempt.Id, settledRef, nowUtc, ct);
            }
            else if (finalState is "PROCESSING")
            {
                activeAttempt.InternalStatus = MentorPayoutAttemptStatus.Processing;
                activeAttempt.ProviderStatus = "PROCESSING";
                _context.MentorPayoutAttempts.Update(activeAttempt);

                obligation.Status = MentorPayoutObligationStatus.Processing;
                obligation.ActiveAttemptId = activeAttempt.Id;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
            }
            else
            {
                activeAttempt.InternalStatus = MentorPayoutAttemptStatus.Failed;
                activeAttempt.ProviderStatus = "FAILED";
                activeAttempt.ErrorMessage = request.Reason?.Trim() ?? "Đối soát xác nhận giao dịch chưa phát sinh/đã thất bại; cho phép thử lại hoặc đổi phương thức chi trả.";
                activeAttempt.CompletedAtUtc = nowUtc;
                _context.MentorPayoutAttempts.Update(activeAttempt);

                obligation.Status = MentorPayoutObligationStatus.FailedRetryable;
                obligation.ActiveAttemptId = null;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
            }

            await _context.SaveChangesAsync(ct);

            var reloaded = await LoadObligationFullAsync(obligation.Id, ct);
            return ApiResponse<MentorPayoutObligationResponse>.Success(
                MapObligation(reloaded!),
                HttpStatusCode.OK,
                "Đối soát trạng thái nghĩa vụ chi trả thành công.");
        }, cancellationToken);
    }

    public async Task<ApiResponse<MentorPayoutObligationResponse>> GetPayoutObligationAsync(
        long obligationId,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Học viên không có quyền xem thông tin chi trả của cố vấn.", HttpStatusCode.Forbidden);

        var obligation = await LoadObligationFullAsync(obligationId, cancellationToken);
        if (obligation is null)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Không tìm thấy nghĩa vụ chi trả.", HttpStatusCode.NotFound);

        if (claim.Role == Role.Mentor && obligation.MentorUserId != claim.Id)
            return ApiResponse<MentorPayoutObligationResponse>.Fail("Cố vấn chỉ được xem nghĩa vụ chi trả của chính mình.", HttpStatusCode.Forbidden);

        return ApiResponse<MentorPayoutObligationResponse>.Success(MapObligation(obligation));
    }

    public async Task<ApiResponse<List<MentorPayoutObligationResponse>>> GetPayoutObligationsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role == Role.Learner)
            return ApiResponse<List<MentorPayoutObligationResponse>>.Fail("Học viên không có quyền xem thông tin chi trả của cố vấn.", HttpStatusCode.Forbidden);

        if (claim.Role == Role.Mentor)
        {
            if (mentorUserId.HasValue && mentorUserId.Value != claim.Id)
                return ApiResponse<List<MentorPayoutObligationResponse>>.Fail("Cố vấn chỉ được xem lịch sử chi trả của chính mình.", HttpStatusCode.Forbidden);
            mentorUserId = claim.Id;
        }

        var query = _context.MentorPayoutObligations
            .Include(o => o.MentorUser)
            .Include(o => o.LockedBankAccount)
            .Include(o => o.Attempts)
            .AsQueryable();

        if (mentorUserId.HasValue)
            query = query.Where(o => o.MentorUserId == mentorUserId.Value);

        var rows = await query
            .OrderByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<MentorPayoutObligationResponse>>.Success(rows.Select(MapObligation).ToList());
    }

    public async Task<ApiResponse<PayOsPayoutBalanceResponse>> GetPayOsPayoutBalanceAsync(
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<PayOsPayoutBalanceResponse>.Fail("Chỉ Quản trị viên mới được xem số dư tài khoản chi trả payOS.", HttpStatusCode.Forbidden);

        var res = await _payOsPayoutClient.GetAccountBalanceAsync(cancellationToken);
        return ApiResponse<PayOsPayoutBalanceResponse>.Success(new PayOsPayoutBalanceResponse
        {
            BalanceVnd = res.BalanceVnd,
            Currency = res.Currency,
            IsQaSimulated = res.IsQaSimulated
        });
    }

    public async Task<ApiResponse<PayOsEstimateCreditResponse>> EstimatePayOsBatchCreditAsync(
        List<long> obligationIds,
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
            return ApiResponse<PayOsEstimateCreditResponse>.Fail("Chỉ Quản trị viên mới được ước tính hạn mức chi trả payOS.", HttpStatusCode.Forbidden);

        var ids = obligationIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
            return ApiResponse<PayOsEstimateCreditResponse>.Fail("Vui lòng chọn ít nhất một nghĩa vụ chi trả để ước tính hạn mức payOS.", HttpStatusCode.BadRequest);

        var obligations = await _context.MentorPayoutObligations
            .Include(o => o.LockedBankAccount)
            .Where(o => ids.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var items = obligations.Select(o => new PayOsBatchPayoutItemCommand(
            ReferenceId: $"est_{o.Id}",
            AmountVnd: (long)decimal.Round(o.PayableAmount, 0, MidpointRounding.AwayFromZero),
            Description: "Estimate",
            ToBin: o.LockedBankAccount?.BankCode ?? "970400",
            ToAccountNumber: "00000000")).ToList();

        var est = await _payOsPayoutClient.EstimateCreditAsync(items, ["salary"], cancellationToken);
        return ApiResponse<PayOsEstimateCreditResponse>.Success(new PayOsEstimateCreditResponse
        {
            TotalAmountVnd = est.TotalAmountVnd,
            EstimatedFeeVnd = est.EstimatedFeeVnd,
            TotalCreditRequiredVnd = est.TotalCreditRequiredVnd,
            Currency = est.Currency,
            IsQaSimulated = est.IsQaSimulated
        });
    }

    // =========================================================================
    // 7. MENTOR SELF-SERVICE COMPENSATION OVERVIEW
    // =========================================================================

    public async Task<ApiResponse<MentorCompensationOverviewResponse>> GetMyCompensationOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var claim = _claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
            return ApiResponse<MentorCompensationOverviewResponse>.Fail("Chỉ Cố vấn mới có thể xem tổng quan thù lao của mình.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;

        var currentRate = await _context.MentorHourlyRates
            .Where(r => r.MentorUserId == claim.Id && r.EffectiveFromUtc <= nowUtc && (r.EffectiveUntilUtc == null || r.EffectiveUntilUtc > nowUtc))
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var qualifications = await _context.MentorTeachingQualifications
            .Include(q => q.CertificateLevel)
            .Where(q => q.MentorUserId == claim.Id && q.IsActive && q.RevokedAtUtc == null)
            .OrderBy(q => q.CertificateLevelId)
            .ToListAsync(cancellationToken);

        var currentBank = await _context.MentorBankAccounts
            .FirstOrDefaultAsync(b => b.MentorUserId == claim.Id && b.IsCurrent, cancellationToken);

        var earnings = await _context.MentorEarningEntries
            .Include(e => e.CertificateLevel)
            .Include(e => e.MentorBooking)
            .Where(e => e.MentorUserId == claim.Id)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var payrolls = await _context.MentorPayrollPeriods
            .Include(p => p.Lines)
                .ThenInclude(l => l.EarningEntry)
            .Include(p => p.Adjustments)
            .Where(p => p.MentorUserId == claim.Id)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Take(12)
            .ToListAsync(cancellationToken);

        var payouts = await _context.MentorPayoutObligations
            .Include(o => o.LockedBankAccount)
            .Include(o => o.Attempts)
            .Where(o => o.MentorUserId == claim.Id)
            .OrderByDescending(o => o.Id)
            .Take(12)
            .ToListAsync(cancellationToken);

        var payoutsByPayrollId = payouts.ToDictionary(o => o.PayrollPeriodId, o => o.Id);

        var overview = new MentorCompensationOverviewResponse
        {
            MentorUserId = claim.Id,
            CurrentHourlyRate = currentRate is null ? null : MapHourlyRate(currentRate, null),
            ActiveQualifications = qualifications.Select(q => MapQualification(q, null, q.CertificateLevel?.Code ?? ResolveLevelCode(q.CertificateLevelId))).ToList(),
            CurrentBankAccount = currentBank is null ? null : MapBankAccount(currentBank),
            TotalEligibleUnassignedVnd = (long)earnings.Where(e => e.Status == MentorEarningStatus.Eligible && e.PayrollPeriodId == null).Sum(e => e.GrossAmount),
            TotalLockedInPayrollVnd = (long)earnings.Where(e => e.Status == MentorEarningStatus.LockedInPayroll).Sum(e => e.GrossAmount),
            TotalPaidVnd = (long)earnings.Where(e => e.Status == MentorEarningStatus.Paid).Sum(e => e.GrossAmount),
            RecentEarnings = earnings.Take(20).Select(e => MapEarning(e, e.CertificateLevel?.Code)).ToList(),
            RecentPayrolls = payrolls.Select(p => MapPayrollPeriod(p, payoutsByPayrollId.TryGetValue(p.Id, out var obId) ? obId : null)).ToList(),
            RecentPayouts = payouts.Select(MapObligation).ToList()
        };

        return ApiResponse<MentorCompensationOverviewResponse>.Success(overview);
    }

    // =========================================================================
    // INTERNAL HELPERS & CRYPTOGRAPHY
    // =========================================================================

    private static readonly System.Threading.SemaphoreSlim FinancialGate = new(1, 1);

    private async Task<ApiResponse<T>> ExecuteInIsolationAsync<T>(
        Func<CancellationToken, Task<ApiResponse<T>>> operation,
        CancellationToken cancellationToken)
    {
        await FinancialGate.WaitAsync(cancellationToken);
        try
        {
            var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
            if (isInMemory)
            {
                return await operation(cancellationToken);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                try
                {
                    var response = await operation(cancellationToken);
                    if (response.IsSuccess)
                    {
                        await tx.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        await tx.RollbackAsync(cancellationToken);
                    }
                    return response;
                }
                catch (DbUpdateException ex)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _logger.LogWarning(ex, "Xung đột đồng thời khi ghi sổ cái tài chính cố vấn.");
                    return ApiResponse<T>.Fail("Thao tác tài chính bị xung đột đồng thời. Vui lòng kiểm tra trạng thái mới nhất và thử lại.", HttpStatusCode.Conflict);
                }
            });
        }
        finally
        {
            FinancialGate.Release();
        }
    }

    private MentorAttendanceApproval UpsertApprovalEntity(
        MentorAttendanceApproval? approval,
        int bookingId,
        int rawMentorSeconds,
        int rawLearnerSeconds,
        int? approvedEligibleMinutes,
        MentorAttendanceApprovalStatus status,
        string reason,
        int reviewedByAdminId,
        DateTime nowUtc)
    {
        var summary = $"MentorSeconds={rawMentorSeconds};LearnerSeconds={rawLearnerSeconds}";
        if (approval is null)
        {
            approval = new MentorAttendanceApproval
            {
                MentorBookingId = bookingId,
                RawMentorDurationSeconds = rawMentorSeconds,
                RawLearnerDurationSeconds = rawLearnerSeconds,
                ApprovedEligibleMinutes = approvedEligibleMinutes,
                EvidenceSummary = summary,
                Status = status,
                DecisionReason = reason,
                ReviewedByAdminId = reviewedByAdminId,
                ReviewedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            _context.MentorAttendanceApprovals.Add(approval);
        }
        else
        {
            approval.RawMentorDurationSeconds = rawMentorSeconds;
            approval.RawLearnerDurationSeconds = rawLearnerSeconds;
            approval.ApprovedEligibleMinutes = approvedEligibleMinutes;
            approval.EvidenceSummary = summary;
            approval.Status = status;
            approval.DecisionReason = reason;
            approval.ReviewedByAdminId = reviewedByAdminId;
            approval.ReviewedAtUtc = nowUtc;
            approval.UpdatedAtUtc = nowUtc;
            _context.MentorAttendanceApprovals.Update(approval);
        }

        return approval;
    }

    private void RecalculatePayrollTotals(MentorPayrollPeriod payroll)
    {
        var totalEarnings = payroll.Lines.Sum(l => l.LineAmount);
        var totalAdjustments = payroll.Adjustments.Sum(a => a.SignedAmount);
        payroll.TotalEarningsAmount = totalEarnings;
        payroll.TotalAdjustmentsAmount = totalAdjustments;
        payroll.GrossPayableAmount = totalEarnings + totalAdjustments;
        _context.MentorPayrollPeriods.Update(payroll);
    }

    private async Task<MentorPayrollPeriod?> LoadPayrollPeriodFullAsync(long payrollPeriodId, CancellationToken ct)
    {
        return await _context.MentorPayrollPeriods
            .Include(p => p.MentorUser)
            .Include(p => p.Lines)
                .ThenInclude(l => l.EarningEntry)
            .Include(p => p.Adjustments)
            .FirstOrDefaultAsync(p => p.Id == payrollPeriodId, ct);
    }

    private async Task<MentorPayoutObligation?> LoadObligationFullAsync(long obligationId, CancellationToken ct)
    {
        return await _context.MentorPayoutObligations
            .Include(o => o.MentorUser)
            .Include(o => o.PayrollPeriod)
            .Include(o => o.LockedBankAccount)
            .Include(o => o.Attempts)
            .FirstOrDefaultAsync(o => o.Id == obligationId, ct);
    }

    private async Task<MentorBankAccount?> GetVerifiedCurrentBankAccountAsync(int mentorUserId, CancellationToken ct)
    {
        return await _context.MentorBankAccounts
            .FirstOrDefaultAsync(b => b.MentorUserId == mentorUserId
                && b.IsCurrent
                && b.VerificationStatus == MentorBankVerificationStatus.Verified, ct);
    }

    private static string? ValidateObligationCanSelectOrSwitchMethod(MentorPayoutObligation obligation)
    {
        if (obligation.Status == MentorPayoutObligationStatus.Paid)
            return "Nghĩa vụ chi trả này đã hoàn tất thanh toán (Paid), không thể thay đổi phương thức.";

        if (obligation.Status == MentorPayoutObligationStatus.UnknownPendingReconciliation)
            return "Nghĩa vụ chi trả đang ở trạng thái chờ đối soát (UnknownPendingReconciliation); bắt buộc đối soát trước khi chọn hoặc đổi phương thức.";

        if (obligation.Status == MentorPayoutObligationStatus.Processing)
            return "Nghĩa vụ chi trả đang ở trạng thái Processing; không được phép đổi phương thức khi giao dịch chưa kết thúc.";

        if (obligation.Attempts.Any(a => a.InternalStatus is MentorPayoutAttemptStatus.Initiated or MentorPayoutAttemptStatus.Processing or MentorPayoutAttemptStatus.UnknownPendingReconciliation or MentorPayoutAttemptStatus.Succeeded))
            return "Đang có lần thử chi trả đang hoạt động hoặc đã thành công; không thể đổi phương thức chi trả.";

        return null;
    }

    private static string? ValidateObligationBeforeExecution(MentorPayoutObligation obligation)
    {
        if (obligation.PayableAmount <= 0)
            return "Số tiền nghĩa vụ chi trả phải lớn hơn 0.";

        if (obligation.PayrollPeriod is not null &&
            obligation.PayrollPeriod.Status is not (MentorPayrollStatus.Approved or MentorPayrollStatus.Paid))
        {
            return "Bảng lương tháng phải được Quản trị viên phê duyệt (Approved) trước khi chi trả.";
        }

        if (obligation.Status == MentorPayoutObligationStatus.Paid)
            return "Nghĩa vụ chi trả này đã được thanh toán hoàn tất (Paid), không thể chi trả lặp lần hai.";

        if (obligation.Status == MentorPayoutObligationStatus.UnknownPendingReconciliation)
            return "Nghĩa vụ chi trả đang chờ đối soát (UnknownPendingReconciliation). Vui lòng hoàn tất đối soát trước khi thử lại hoặc đổi phương thức.";

        if (obligation.Status == MentorPayoutObligationStatus.Processing)
            return "Nghĩa vụ chi trả đang ở trạng thái Processing; không thể khởi tạo giao dịch chi trả chồng chéo.";

        if (obligation.Attempts.Any(a => a.InternalStatus == MentorPayoutAttemptStatus.Succeeded))
            return "Nghĩa vụ chi trả này đã có lần chi trả thành công (Succeeded).";

        if (obligation.Attempts.Any(a => a.InternalStatus is MentorPayoutAttemptStatus.Initiated or MentorPayoutAttemptStatus.Processing or MentorPayoutAttemptStatus.UnknownPendingReconciliation))
            return "Đang có một lần chi trả chưa kết thúc cho nghĩa vụ này.";

        return null;
    }

    private static MentorPayoutAttemptStatus ResolveAttemptStatusFromProvider(PayOsPayoutTransactionResult providerResult)
    {
        if (providerResult.IsTimeoutOrUnknown ||
            string.Equals(providerResult.ApprovalState, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            return MentorPayoutAttemptStatus.UnknownPendingReconciliation;
        }

        var approval = providerResult.ApprovalState.Trim().ToUpperInvariant();
        var txState = providerResult.TransactionState.Trim().ToUpperInvariant();

        if (approval is "COMPLETED" or "SUCCEEDED" && txState is "SUCCEEDED" or "COMPLETED")
            return MentorPayoutAttemptStatus.Succeeded;

        if (approval is "PROCESSING" or "PENDING" or "APPROVED" || txState is "PROCESSING" or "PENDING")
            return MentorPayoutAttemptStatus.Processing;

        return MentorPayoutAttemptStatus.Failed;
    }

    private async Task ApplyAttemptOutcomeToObligationAsync(
        MentorPayoutObligation obligation,
        MentorPayoutAttempt attempt,
        string settledReference,
        DateTime nowUtc,
        CancellationToken ct)
    {
        switch (attempt.InternalStatus)
        {
            case MentorPayoutAttemptStatus.Succeeded:
                await MarkObligationAndPayrollPaidAsync(obligation, attempt.Id, settledReference, nowUtc, ct);
                break;

            case MentorPayoutAttemptStatus.Processing:
                obligation.Status = MentorPayoutObligationStatus.Processing;
                obligation.ActiveAttemptId = attempt.Id;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
                break;

            case MentorPayoutAttemptStatus.UnknownPendingReconciliation:
                obligation.Status = MentorPayoutObligationStatus.UnknownPendingReconciliation;
                obligation.ActiveAttemptId = attempt.Id;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
                break;

            default:
                obligation.Status = MentorPayoutObligationStatus.FailedRetryable;
                obligation.ActiveAttemptId = null;
                obligation.UpdatedAtUtc = nowUtc;
                _context.MentorPayoutObligations.Update(obligation);
                break;
        }
    }

    private async Task MarkObligationAndPayrollPaidAsync(
        MentorPayoutObligation obligation,
        long succeededAttemptId,
        string settledReference,
        DateTime nowUtc,
        CancellationToken ct)
    {
        obligation.Status = MentorPayoutObligationStatus.Paid;
        obligation.ActiveAttemptId = null;
        obligation.SettledAttemptId = succeededAttemptId;
        obligation.SettledReference = settledReference;
        obligation.SettledAtUtc = nowUtc;
        obligation.UpdatedAtUtc = nowUtc;
        _context.MentorPayoutObligations.Update(obligation);

        var payroll = await _context.MentorPayrollPeriods
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == obligation.PayrollPeriodId, ct);

        if (payroll is not null)
        {
            payroll.Status = MentorPayrollStatus.Paid;
            payroll.ClosedAtUtc = nowUtc;
            _context.MentorPayrollPeriods.Update(payroll);

            var earningIds = payroll.Lines.Select(l => l.EarningEntryId).ToList();
            if (earningIds.Count > 0)
            {
                var earnings = await _context.MentorEarningEntries
                    .Where(e => earningIds.Contains(e.Id))
                    .ToListAsync(ct);

                foreach (var earning in earnings)
                {
                    earning.Status = MentorEarningStatus.Paid;
                    _context.MentorEarningEntries.Update(earning);
                }
            }
        }
    }

    private async Task<int> ResolveCertificateLevelIdAsync(string normalizedLevel, CancellationToken ct)
    {
        var certLevel = await _context.CertificateLevels
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code.ToUpper() == normalizedLevel, ct);
        return certLevel?.Id ?? MentorCompensationPolicy.ResolveDefaultCertificateLevelId(normalizedLevel);
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeAccountNumber(string raw)
        => new((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    public static string MaskAccountNumber(string cleanAccountNo)
    {
        if (string.IsNullOrWhiteSpace(cleanAccountNo))
            return "****";
        var trimmed = cleanAccountNo.Trim();
        return trimmed.Length <= 4 ? $"****{trimmed}" : $"****{trimmed[^4..]}";
    }

    private byte[] GetEncryptionKeyBytes()
    {
        var keyMaterial = _appSettings.MentorBankSecurity?.EncryptionKey;
        if (string.IsNullOrWhiteSpace(keyMaterial))
            keyMaterial = _appSettings.SecretToken?.Value ?? "rikipath-mentor-bank-default-key-32bytes!";
        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    private string EncryptAccountNumber(string plainText)
    {
        var key = GetEncryptionKeyBytes();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(combined);
    }

    private string DecryptAccountNumber(string cipherBase64)
    {
        var key = GetEncryptionKeyBytes();
        var combined = Convert.FromBase64String(cipherBase64);
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        var cipherBytes = new byte[combined.Length - 16];
        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, cipherBytes, 0, cipherBytes.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private string HashAccountNumber(string bankBin, string cleanAccountNo)
    {
        var pepper = _appSettings.MentorBankSecurity?.EncryptionKey;
        if (string.IsNullOrWhiteSpace(pepper))
            pepper = "rikipath-mentor-bank-hash-pepper";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{bankBin}:{cleanAccountNo}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveLevelCode(int certificateLevelId)
        => MentorCompensationPolicy.ResolveDefaultLevelCode(certificateLevelId) ?? $"L{certificateLevelId}";

    // =========================================================================
    // MAPPERS
    // =========================================================================

    private static MentorHourlyRateResponse MapHourlyRate(MentorHourlyRate r, UserAccount? mentor) => new()
    {
        Id = r.Id,
        MentorUserId = r.MentorUserId,
        MentorName = mentor is null ? null : $"{mentor.FirstName} {mentor.LastName}".Trim(),
        HourlyRateVnd = r.AmountPerHour,
        Currency = "VND",
        EffectiveFromUtc = r.EffectiveFromUtc,
        EffectiveToUtc = r.EffectiveUntilUtc,
        IsActive = r.EffectiveUntilUtc == null || r.EffectiveUntilUtc > DateTime.UtcNow,
        ApprovedByUserId = r.ConfiguredByAdminId,
        ApprovedAtUtc = r.CreatedAtUtc
    };

    private static JlptLevelPayCoefficientResponse MapCoefficient(JlptLevelPayCoefficient c, string jlptLevel) => new()
    {
        Id = c.Id,
        CertificateLevelId = c.CertificateLevelId,
        JlptLevel = jlptLevel,
        Coefficient = c.Coefficient,
        EffectiveFromUtc = c.EffectiveFromUtc,
        EffectiveToUtc = c.EffectiveUntilUtc,
        IsActive = c.EffectiveUntilUtc == null || c.EffectiveUntilUtc > DateTime.UtcNow,
        ApprovedByUserId = c.ConfiguredByAdminId,
        ApprovedAtUtc = c.CreatedAtUtc
    };

    private static MentorTeachingQualificationResponse MapQualification(
        MentorTeachingQualification q,
        UserAccount? mentor,
        string jlptLevel) => new()
    {
        Id = q.Id,
        MentorUserId = q.MentorUserId,
        MentorName = mentor is null ? null : $"{mentor.FirstName} {mentor.LastName}".Trim(),
        CertificateLevelId = q.CertificateLevelId,
        JlptLevel = jlptLevel,
        IsActive = q.IsActive,
        ApprovedByUserId = q.VerifiedByAdminId,
        ApprovedAtUtc = q.VerifiedAtUtc,
        RevokedAtUtc = q.RevokedAtUtc
    };

    private static MentorAttendanceApprovalResponse MapAttendanceApproval(
        MentorAttendanceApproval a,
        MentorBooking? booking,
        MentorEarningEntry? earning,
        string? levelCode = null) => new()
    {
        Id = a.Id,
        MentorBookingId = a.MentorBookingId,
        MentorUserId = booking?.MentorAvailability?.MentorId ?? booking?.UserSubscription?.MentorId ?? earning?.MentorUserId ?? 0,
        LearnerUserId = booking?.UserSubscription?.UserId ?? 0,
        ScheduledMinutes = MentorSlotPolicy.AppointmentDurationMinutes,
        MentorObservedSeconds = a.RawMentorDurationSeconds,
        LearnerObservedSeconds = a.RawLearnerDurationSeconds,
        ApprovedEligibleMinutes = a.ApprovedEligibleMinutes ?? 0,
        Status = a.Status,
        DecisionReason = a.DecisionReason,
        ReviewedByUserId = a.ReviewedByAdminId,
        ReviewedAtUtc = a.ReviewedAtUtc,
        EarningEntry = earning is null ? null : MapEarning(earning, levelCode ?? earning.JlptLevelCode)
    };

    private static MentorEarningEntryResponse MapEarning(MentorEarningEntry e, string? levelCode = null)
    {
        var scheduledAmt = e.MentorBooking?.ScheduledCompensationAmount
            ?? MentorCompensationPolicy.CalculateEligibleEarning(e.FrozenHourlyRate, e.FrozenLevelCoefficient, MentorSlotPolicy.AppointmentDurationMinutes);
        return new MentorEarningEntryResponse
        {
            Id = e.Id,
            MentorUserId = e.MentorUserId,
            MentorBookingId = e.MentorBookingId,
            AttendanceApprovalId = e.AttendanceApprovalId,
            CertificateLevelId = e.CertificateLevelId,
            JlptLevel = !string.IsNullOrWhiteSpace(e.JlptLevelCode) ? e.JlptLevelCode : (levelCode ?? ResolveLevelCode(e.CertificateLevelId)),
            FrozenHourlyRateVnd = (long)decimal.Round(e.FrozenHourlyRate, 0, MidpointRounding.AwayFromZero),
            FrozenLevelCoefficient = e.FrozenLevelCoefficient,
            ScheduledMinutes = MentorSlotPolicy.AppointmentDurationMinutes,
            ApprovedEligibleMinutes = e.ApprovedEligibleMinutes,
            ScheduledAmountVnd = (long)decimal.Round(scheduledAmt, 0, MidpointRounding.AwayFromZero),
            EligibleAmountVnd = (long)decimal.Round(e.GrossAmount, 0, MidpointRounding.AwayFromZero),
            Currency = e.Currency,
            FormulaVersion = e.FormulaVersion,
            Status = e.Status,
            MentorPayrollLineId = e.PayrollPeriodId,
            OperationKey = e.OperationKey,
            EarnedAtUtc = e.CreatedAtUtc
        };
    }

    private static MentorPayrollAdjustmentResponse MapAdjustment(MentorPayrollAdjustment a, int mentorUserId) => new()
    {
        Id = a.Id,
        MentorPayrollPeriodId = a.PayrollPeriodId,
        MentorUserId = mentorUserId,
        RelatedMentorBookingId = a.RelatedBookingId,
        AmountVnd = (long)decimal.Round(a.SignedAmount, 0, MidpointRounding.AwayFromZero),
        Reason = a.Reason,
        OperationKey = a.OperationKey,
        CreatedByUserId = a.ApprovedByAdminId,
        ApprovedByUserId = a.ApprovedByAdminId,
        ApprovedAtUtc = a.CreatedAtUtc,
        CreatedAtUtc = a.CreatedAtUtc
    };

    private static MentorPayrollPeriodResponse MapPayrollPeriod(MentorPayrollPeriod p, long? payoutObligationId)
    {
        var periodStartUtc = new DateTime(p.Year, p.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddMonths(1);
        return new MentorPayrollPeriodResponse
        {
            Id = p.Id,
            MentorUserId = p.MentorUserId,
            MentorName = p.MentorUser is null ? null : $"{p.MentorUser.FirstName} {p.MentorUser.LastName}".Trim(),
            PeriodYear = p.Year,
            PeriodMonth = p.Month,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            Status = p.Status,
            TotalEarningsAmountVnd = (long)decimal.Round(p.TotalEarningsAmount, 0, MidpointRounding.AwayFromZero),
            TotalAdjustmentsAmountVnd = (long)decimal.Round(p.TotalAdjustmentsAmount, 0, MidpointRounding.AwayFromZero),
            GrossPayableAmountVnd = (long)decimal.Round(p.GrossPayableAmount, 0, MidpointRounding.AwayFromZero),
            Currency = p.Currency,
            ApprovedByUserId = p.ApprovedByAdminId,
            ApprovedAtUtc = p.ApprovedAtUtc,
            Notes = p.ApprovalNotes,
            PayoutObligationId = payoutObligationId,
            Lines = p.Lines.OrderBy(l => l.Id).Select(l => new MentorPayrollLineResponse
            {
                Id = l.Id,
                MentorPayrollPeriodId = l.PayrollPeriodId,
                MentorEarningEntryId = l.EarningEntryId,
                MentorBookingId = l.MentorBookingId,
                CertificateLevelId = l.EarningEntry?.CertificateLevelId ?? 0,
                JlptLevel = l.EarningEntry?.JlptLevelCode ?? ResolveLevelCode(l.EarningEntry?.CertificateLevelId ?? 0),
                FrozenHourlyRateVnd = (long)decimal.Round(l.FrozenHourlyRate, 0, MidpointRounding.AwayFromZero),
                FrozenLevelCoefficient = l.FrozenLevelCoefficient,
                ApprovedEligibleMinutes = l.ApprovedEligibleMinutes,
                LineAmountVnd = (long)decimal.Round(l.LineAmount, 0, MidpointRounding.AwayFromZero),
                CreatedAtUtc = l.CreatedAtUtc
            }).ToList(),
            Adjustments = p.Adjustments.OrderBy(a => a.Id).Select(a => MapAdjustment(a, p.MentorUserId)).ToList()
        };
    }

    private static MentorBankAccountResponse MapBankAccount(MentorBankAccount b) => new()
    {
        Id = b.Id,
        MentorUserId = b.MentorUserId,
        BankBin = b.BankCode,
        BankShortName = b.BankName,
        MaskedAccountNumber = b.MaskedAccountNumber,
        AccountHolderName = b.AccountHolderName,
        VerificationStatus = b.VerificationStatus,
        VerificationMethod = b.VerificationNotes,
        VerifiedByUserId = b.VerifiedByAdminId,
        VerifiedAtUtc = b.VerifiedAtUtc,
        AccountVersion = b.AccountVersion,
        IsCurrent = b.IsCurrent,
        Notes = b.VerificationNotes
    };

    private static OrganizationPayoutAccountResponse MapOrganizationAccount(OrganizationPayoutAccount o) => new()
    {
        Id = o.Id,
        DisplayName = o.ProviderAccountReference ?? o.ChannelCode,
        BankBin = o.BankCode,
        BankShortName = o.BankName,
        MaskedAccountNumber = o.MaskedAccountNumber,
        AccountHolderName = o.AccountHolderName,
        SupportsManualTransfer = true,
        SupportsPayOsPayout = o.ProviderType == PayoutExecutionMethod.PayOsPayout,
        PayOsPayoutChannelCode = o.ChannelCode,
        IsActive = o.IsActive
    };

    private static MentorPayoutObligationResponse MapObligation(MentorPayoutObligation o)
    {
        var payableLong = (long)decimal.Round(o.PayableAmount, 0, MidpointRounding.AwayFromZero);
        var lastFailed = o.Attempts.OrderByDescending(a => a.AttemptNumber).FirstOrDefault(a => a.InternalStatus == MentorPayoutAttemptStatus.Failed);
        return new MentorPayoutObligationResponse
        {
            Id = o.Id,
            MentorPayrollPeriodId = o.PayrollPeriodId,
            MentorUserId = o.MentorUserId,
            MentorName = o.MentorUser is null ? null : $"{o.MentorUser.FirstName} {o.MentorUser.LastName}".Trim(),
            MentorBankAccountId = o.LockedBankAccountId,
            OrganizationPayoutAccountId = o.OrganizationPayoutAccountId,
            SelectedExecutionMethod = o.SelectedMethod,
            Status = o.Status,
            PayableAmountVnd = payableLong,
            PaidAmountVnd = o.Status == MentorPayoutObligationStatus.Paid ? payableLong : 0,
            Currency = o.Currency,
            RecipientBankBinSnapshot = o.LockedBankAccount?.BankCode ?? string.Empty,
            RecipientBankShortNameSnapshot = o.LockedBankAccount?.BankName ?? string.Empty,
            RecipientMaskedAccountSnapshot = o.LockedBankAccount?.MaskedAccountNumber ?? string.Empty,
            RecipientAccountHolderSnapshot = o.LockedBankAccount?.AccountHolderName ?? string.Empty,
            PaidByAttemptId = o.SettledAttemptId,
            PaidAtUtc = o.SettledAtUtc,
            FailureReason = lastFailed?.ErrorMessage,
            Attempts = o.Attempts.OrderBy(a => a.AttemptNumber).Select(a => new MentorPayoutAttemptResponse
            {
                Id = a.Id,
                MentorPayoutObligationId = a.PayoutObligationId,
                AttemptNumber = a.AttemptNumber,
                ExecutionMethod = a.ExecutionMethod,
                Status = a.InternalStatus,
                AmountVnd = (long)decimal.Round(a.Amount, 0, MidpointRounding.AwayFromZero),
                Currency = a.Currency,
                IdempotencyKey = a.IdempotencyKey,
                RequestCorrelationId = a.ProviderPayoutId ?? a.IdempotencyKey,
                ProviderName = a.ExecutionMethod == PayoutExecutionMethod.PayOsPayout ? "PAYOS" : "MANUAL_BANK",
                ProviderPayoutId = a.ProviderPayoutId,
                ProviderBatchId = a.BatchId,
                ProviderTransactionId = a.TransferReference,
                ProviderReferenceId = a.ProviderPayoutId,
                ProviderApprovalState = a.ProviderStatus,
                ProviderErrorCode = a.ErrorCode,
                ProviderErrorMessage = a.ErrorMessage,
                TransferReference = a.TransferReference,
                EvidenceStorageKey = a.EvidenceStorageKey,
                InitiatedByUserId = a.InitiatedByAdminId,
                InitiatedAtUtc = a.CreatedAtUtc,
                ConfirmedByUserId = a.ConfirmedByAdminId,
                ConfirmedAtUtc = a.CompletedAtUtc,
                CompletedAtUtc = a.CompletedAtUtc,
                Notes = a.EvidenceNotes
            }).ToList()
        };
    }
}
