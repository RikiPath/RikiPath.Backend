using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorAvailabilityReviewService(IUnitOfWork unitOfWork, IClaimService claimService)
    : IMentorAvailabilityReviewService
{
    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> SplitAsync(int id, int sessionMinutes, CancellationToken ct)
    {
        var actor = claimService.GetUserClaim();
        if (actor.Role != Role.Admin) return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Admin được chia khung rảnh.", HttpStatusCode.Forbidden);
        if (sessionMinutes is < 15 or > 240) return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Thời lượng mỗi buổi phải từ 15 đến 240 phút.");
        var source = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, ct);
        if (source is null) return ApiResponse<List<MentorAvailabilityResponse>>.NotFound();
        var result = new List<MentorAvailability>();
        var error = "";
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.StudyCourses.LockScheduleAsync(source.MentorId, source.MentorId, token);
            var state = await unitOfWork.MentorAvailabilities.Query().AsNoTracking().SingleAsync(s => s.Id == id, token);
            if (state.IsDeleted || !state.IsApproved || state.RejectedAt != null || state.PolicyVersion != null || state.GroupPolicyId != null
                || state.ChangeRequestedAt != null || state.BookedCount > 0 || state.MaxLearners != 1 || state.StartTime <= DateTime.UtcNow
                || state.EndTime - state.StartTime <= TimeSpan.FromMinutes(sessionMinutes)
                || await unitOfWork.StudyCohorts.AnyAsync(c => c.Sessions.Any(s => s.AvailabilityId == id), token)
                || await unitOfWork.StudyCourses.AnyAsync(c => c.Sessions.Any(s => s.AvailabilityId == id), token)
                || await unitOfWork.MentorBookings.AnyAsync(b => b.MentorAvailabilityId == id && !b.IsDeleted
                    && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted
                        || (b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentExpiresAt > DateTime.UtcNow)), token))
            { error = "Chỉ chia khung đã duyệt, chưa đặt/gán lớp, không có yêu cầu điều chỉnh và dài hơn một buổi."; return; }
            for (var start = state.StartTime; start < state.EndTime; start = start.AddMinutes(sessionMinutes))
            {
                var end = start.AddMinutes(sessionMinutes);
                if (end > state.EndTime) end = state.EndTime;
                result.Add(new() { MentorId = state.MentorId, StartTime = start, EndTime = end, IsApproved = true,
                    PublishedAt = DateTime.UtcNow, AdminName = $"Admin #{actor.Id}",
                    ChangeResolution = $"Chia từ khung #{id}; thời lượng cấu hình {sessionMinutes} phút. Phần lẻ giữ nguyên." });
            }
            source.IsDeleted = true; source.ModifiedDate = DateTime.UtcNow;
            source.ChangeResolution = $"Admin #{actor.Id} chia thành {result.Count} khung; mỗi buổi {sessionMinutes} phút.";
            unitOfWork.MentorAvailabilities.Update(source);
            await unitOfWork.MentorAvailabilities.AddRangeAsync(result, token);
            await unitOfWork.SaveChangesAsync(token);
        }, ct);
        return error.Length > 0 ? ApiResponse<List<MentorAvailabilityResponse>>.Fail(error, HttpStatusCode.Conflict)
            : ApiResponse<List<MentorAvailabilityResponse>>.Success(result.Select(s => Map(s, $"Mentor #{s.MentorId}")).ToList());
    }

    public async Task<ApiResponse<List<AdminMentorScheduleResponse>>> GetScheduleAsync(int mentorId, DateTime from, DateTime to, CancellationToken ct)
    {
        if (claimService.GetUserClaim().Role != Role.Admin)
            return ApiResponse<List<AdminMentorScheduleResponse>>.Fail("Chỉ quản trị viên được xem lịch tư vấn chi tiết của cố vấn.", HttpStatusCode.Forbidden);
        if (mentorId <= 0 || to <= from || to - from > TimeSpan.FromDays(31))
            return ApiResponse<List<AdminMentorScheduleResponse>>.Fail("Vui lòng chọn cố vấn và khoảng thời gian hợp lệ, tối đa 31 ngày.");
        var mentor = await unitOfWork.UserAccounts.GetByIdAsync(mentorId, ct);
        if (mentor is null || mentor.Role != Role.Mentor)
            return ApiResponse<List<AdminMentorScheduleResponse>>.NotFound("Không tìm thấy cố vấn.");
        var slots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Include(s => s.MentorBookings).ThenInclude(b => b.UserSubscription).ThenInclude(s => s.UserAccount)
            .Where(s => s.MentorId == mentorId && s.StartTime >= from && s.StartTime < to)
            .OrderBy(s => s.StartTime).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var slotIds = slots.Select(s => s.Id).ToArray();
        var pendingReservations = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Where(s => !s.IsDeleted && s.PaymentStatus == PaymentStatus.Pending
                && s.PendingMentorAvailabilityId != null && slotIds.Contains(s.PendingMentorAvailabilityId.Value))
            .Select(s => new { SlotId = s.PendingMentorAvailabilityId!.Value, s.PaymentExpiresAt })
            .ToListAsync(ct);
        var reservationsBySlot = pendingReservations.ToLookup(s => s.SlotId);
        return ApiResponse<List<AdminMentorScheduleResponse>>.Success(slots.Select(s => {
            var bookings = s.MentorBookings.Where(b => !b.IsDeleted && !b.UserSubscription.IsDeleted).ToList();
            var paid = bookings.Any(b => b.UserSubscription.PaymentStatus == PaymentStatus.Paid
                && b.Status is MentorStatus.Assigned or MentorStatus.Accepted or MentorStatus.Completed);
            var held = bookings.Any(b => b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentStatus == PaymentStatus.Pending
                && b.UserSubscription.PaymentExpiresAt > now)
                || reservationsBySlot[s.Id].Any(r => r.PaymentExpiresAt > now);
            var expiredHold = bookings.Any(b => b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentStatus == PaymentStatus.Pending
                && b.UserSubscription.PaymentExpiresAt <= now)
                || reservationsBySlot[s.Id].Any(r => r.PaymentExpiresAt <= now);
            var status = s.IsDeleted ? "Cancelled" : s.RejectedAt != null ? "Rejected" : !s.IsApproved ? "Draft" :
                s.EndTime <= now ? "Ended" : paid ? "Booked" : held ? "Held" : expiredHold ? "HeldExpired" : "Available";
            return new AdminMentorScheduleResponse(s.Id, s.StartTime, s.EndTime, status, bookings
                .Select(b => new AdminMentorScheduleBookingResponse(b.Id, b.UserSubscription.UserId,
                    $"{b.UserSubscription.UserAccount.FirstName} {b.UserSubscription.UserAccount.LastName}".Trim(),
                    b.Question, b.Status.ToString(), b.UserSubscription.PaymentStatus.ToString())).ToList());
        }).ToList());
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetSlotsAsync(CancellationToken cancellationToken)
    {
        if (claimService.GetUserClaim().Role != Role.Admin)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ quản trị viên mới được duyệt lịch tư vấn của cố vấn.", HttpStatusCode.Forbidden);
        var slots = await unitOfWork.MentorAvailabilities.FindAsync(x => !x.IsDeleted, cancellationToken);
        var slotIds = slots.Select(x => x.Id).ToList();
        var cohortMap = slotIds.Count == 0
            ? new Dictionary<int, int>()
            : (await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .SelectMany(c => c.Sessions.Where(s => slotIds.Contains(s.AvailabilityId)).Select(s => new { s.AvailabilityId, CohortId = c.Id }))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.AvailabilityId)
                .ToDictionary(g => g.Key, g => g.First().CohortId);
        var mentors = await unitOfWork.UserAccounts.FindAsync(x => x.Role == Role.Mentor, cancellationToken);
        var names = mentors.ToDictionary(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim());
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .OrderBy(x => x.IsApproved || x.RejectedAt != null).ThenBy(x => x.StartTime)
            .Select(x => Map(x, names.GetValueOrDefault(x.MentorId) ?? "Cố vấn chưa cập nhật tên",
                cohortMap.TryGetValue(x.Id, out var cid) ? cid : null)).ToList());
    }

    public async Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ quản trị viên mới được duyệt lịch tư vấn của cố vấn.", HttpStatusCode.Forbidden);
        var normalizedReason = reason?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(normalizedReason))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Vui lòng nhập lý do từ chối.");
        if (normalizedReason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do tối đa 1000 ký tự.");
        var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
        if (slot is null || slot.IsDeleted)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy lịch tư vấn của cố vấn.", HttpStatusCode.NotFound);

        if (slot.IsApproved || slot.RejectedAt != null)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lịch đã được xử lý. Vui lòng tải lại danh sách.", HttpStatusCode.Conflict);
        if (slot.StartTime <= DateTime.UtcNow)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể duyệt lịch đã bắt đầu hoặc hết hạn.", HttpStatusCode.Conflict);
        if (slot.BookedCount > 0)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể thay đổi duyệt lịch đã có học viên đặt.", HttpStatusCode.Conflict);
        if (approve && await unitOfWork.MentorAvailabilities.Query().AnyAsync(x =>
                x.Id != slot.Id && x.MentorId == slot.MentorId && !x.IsDeleted && x.IsApproved && x.RejectedAt == null
                && x.StartTime < slot.EndTime && slot.StartTime < x.EndTime, cancellationToken))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Cố vấn đã có khung giờ khác được duyệt trùng thời gian này.", HttpStatusCode.Conflict);
        var mentor = await unitOfWork.UserAccounts.GetByIdAsync(slot.MentorId, cancellationToken);
        if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Cố vấn này không còn hoạt động.", HttpStatusCode.Conflict);
        var admin = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
        slot.IsApproved = approve;
        slot.PublishedAt = approve ? DateTime.UtcNow : null;
        slot.AdminName = admin is null ? $"Admin #{claims.Id}" : $"{admin.FirstName} {admin.LastName}".Trim();
        slot.RejectedAt = approve ? null : DateTime.UtcNow;
        slot.RejectionReason = approve ? null : normalizedReason;
        slot.ModifiedDate = DateTime.UtcNow;
        unitOfWork.MentorAvailabilities.Update(slot);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<MentorAvailabilityResponse>.Success(Map(slot, $"{mentor.FirstName} {mentor.LastName}".Trim()));
    }

    private static MentorAvailabilityResponse Map(MentorAvailability slot, string mentorName, int? cohortId = null) => new()
    {
        Id = slot.Id, MentorId = slot.MentorId, MentorName = mentorName,
        StartTime = slot.StartTime, EndTime = slot.EndTime, IsApproved = slot.IsApproved,
        RejectedAt = slot.RejectedAt, RejectionReason = slot.RejectionReason,
        IsBooked = slot.IsFull, BookedCount = slot.BookedCount, MaxLearners = slot.MaxLearners,
        AdminName = slot.AdminName,
        CancelRequestedAt = slot.CancelRequestedAt, CancelReason = slot.CancelReason,
        CancelRejectedAt = slot.CancelRejectedAt, CancelRejectionReason = slot.CancelRejectionReason,
        MentorWeeklyAvailabilityId = slot.MentorWeeklyAvailabilityId, PublishedAt = slot.PublishedAt,
        ChangeRequestReason = slot.ChangeRequestReason, ChangeRequestedAt = slot.ChangeRequestedAt,
        ChangeResolution = slot.ChangeResolution,
        IsInCohort = cohortId.HasValue, CohortId = cohortId
    };
}
