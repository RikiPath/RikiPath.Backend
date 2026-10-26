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
    public async Task<ApiResponse<List<AdminMentorScheduleResponse>>> GetScheduleAsync(int mentorId, DateTime from, DateTime to, CancellationToken ct)
    {
        if (claimService.GetUserClaim().Role != Role.Admin)
            return ApiResponse<List<AdminMentorScheduleResponse>>.Fail("Chỉ admin được xem lịch chi tiết mentor.", HttpStatusCode.Forbidden);
        if (mentorId <= 0 || to <= from || to - from > TimeSpan.FromDays(31))
            return ApiResponse<List<AdminMentorScheduleResponse>>.Fail("Chọn mentor và khoảng thời gian hợp lệ, tối đa 31 ngày.");
        var mentor = await unitOfWork.UserAccounts.GetByIdAsync(mentorId, ct);
        if (mentor is null || mentor.Role != Role.Mentor)
            return ApiResponse<List<AdminMentorScheduleResponse>>.NotFound("Không tìm thấy mentor.");
        var slots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Include(s => s.MentorBookings).ThenInclude(b => b.UserSubscription).ThenInclude(s => s.UserAccount)
            .Where(s => s.MentorId == mentorId && s.StartTime >= from && s.StartTime < to)
            .OrderBy(s => s.StartTime).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return ApiResponse<List<AdminMentorScheduleResponse>>.Success(slots.Select(s => {
            var bookings = s.MentorBookings.Where(b => !b.IsDeleted && !b.UserSubscription.IsDeleted).ToList();
            var paid = bookings.Any(b => b.UserSubscription.PaymentStatus == PaymentStatus.Paid
                && b.Status is MentorStatus.Assigned or MentorStatus.Accepted or MentorStatus.Completed);
            var held = bookings.Any(b => b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentStatus == PaymentStatus.Pending
                && b.UserSubscription.PaymentExpiresAt > now);
            var expiredHold = bookings.Any(b => b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentStatus == PaymentStatus.Pending
                && b.UserSubscription.PaymentExpiresAt <= now);
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
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Admin mới được duyệt lịch Mentor.", HttpStatusCode.Forbidden);
        var slots = await unitOfWork.MentorAvailabilities.FindAsync(x => !x.IsDeleted, cancellationToken);
        var mentors = await unitOfWork.UserAccounts.FindAsync(x => x.Role == Role.Mentor, cancellationToken);
        var names = mentors.ToDictionary(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim());
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .OrderBy(x => x.IsApproved || x.RejectedAt != null).ThenBy(x => x.StartTime)
            .Select(x => Map(x, names.GetValueOrDefault(x.MentorId) ?? $"Mentor #{x.MentorId}")).ToList());
    }

    public async Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ Admin mới được duyệt lịch Mentor.", HttpStatusCode.Forbidden);
        var normalizedReason = reason?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(normalizedReason))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Vui lòng nhập lý do từ chối.");
        if (normalizedReason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do tối đa 1000 ký tự.");
        var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
        if (slot is null || slot.IsDeleted)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy lịch Mentor.", HttpStatusCode.NotFound);
        if (slot.MentorWeeklyAvailabilityId != null)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Dùng Công bố lịch tuần để kiểm tra đủ 5 tiết trước khi mở đặt lịch.");
        if (slot.IsApproved || slot.RejectedAt != null)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lịch đã được xử lý. Vui lòng tải lại danh sách.", HttpStatusCode.Conflict);
        if (slot.StartTime <= DateTime.UtcNow)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể duyệt lịch đã bắt đầu hoặc hết hạn.", HttpStatusCode.Conflict);
        if (slot.BookedCount > 0)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể thay đổi duyệt lịch đã có học viên đặt.", HttpStatusCode.Conflict);
        var mentor = await unitOfWork.UserAccounts.GetByIdAsync(slot.MentorId, cancellationToken);
        if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Mentor không còn hoạt động.", HttpStatusCode.Conflict);
        var admin = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
        slot.IsApproved = approve;
        slot.AdminName = admin is null ? $"Admin #{claims.Id}" : $"{admin.FirstName} {admin.LastName}".Trim();
        slot.RejectedAt = approve ? null : DateTime.UtcNow;
        slot.RejectionReason = approve ? null : normalizedReason;
        slot.ModifiedDate = DateTime.UtcNow;
        unitOfWork.MentorAvailabilities.Update(slot);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<MentorAvailabilityResponse>.Success(Map(slot, $"{mentor.FirstName} {mentor.LastName}".Trim()));
    }

    private static MentorAvailabilityResponse Map(MentorAvailability slot, string mentorName) => new()
    {
        Id = slot.Id, MentorId = slot.MentorId, MentorName = mentorName,
        StartTime = slot.StartTime, EndTime = slot.EndTime, IsApproved = slot.IsApproved,
        RejectedAt = slot.RejectedAt, RejectionReason = slot.RejectionReason,
        IsBooked = slot.IsFull, BookedCount = slot.BookedCount, MaxLearners = slot.MaxLearners,
        AdminName = slot.AdminName
        , MentorWeeklyAvailabilityId = slot.MentorWeeklyAvailabilityId, PublishedAt = slot.PublishedAt,
        ChangeRequestReason = slot.ChangeRequestReason, ChangeRequestedAt = slot.ChangeRequestedAt,
        ChangeResolution = slot.ChangeResolution
    };
}
