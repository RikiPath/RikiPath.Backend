using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorScheduleService(IUnitOfWork uow, IClaimService claims, IEmailService email,
    ILogger<MentorScheduleService> logger) : IMentorScheduleService
{
    private bool IsAdmin => claims.GetUserClaim().Role == Role.Admin;
    private static ApiResponse<bool> Fail(string message) => ApiResponse<bool>.Fail(message);

    public async Task<ApiResponse<List<WeeklyAvailabilityResponse>>> GetWeekAsync(DateOnly weekStart, CancellationToken ct)
    {
        var user = claims.GetUserClaim();
        if (user.Role is not (Role.Admin or Role.Mentor))
            return ApiResponse<List<WeeklyAvailabilityResponse>>.Fail("Không có quyền xem đăng ký.", HttpStatusCode.Forbidden);
        var rows = await uow.MentorWeeklyAvailabilities.Query().Include(x => x.Mentor)
            .Where(x => !x.IsDeleted && x.Date >= weekStart && x.Date < weekStart.AddDays(7)
                && (user.Role == Role.Admin || x.MentorId == user.Id))
            .OrderBy(x => x.Date).ThenBy(x => x.MentorId).ToListAsync(ct);
        return ApiResponse<List<WeeklyAvailabilityResponse>>.Success(rows.Select(x => new WeeklyAvailabilityResponse(
            x.Id, x.MentorId, $"{x.Mentor.FirstName} {x.Mentor.LastName}".Trim(), x.Date, x.Period)).ToList());
    }

    public async Task<ApiResponse<bool>> SaveWeekAsync(SaveWeeklyAvailabilityRequest request, CancellationToken ct)
    {
        var user = claims.GetUserClaim();
        if (user.Role is not (Role.Mentor or Role.Admin)) return Fail("Không có quyền đăng ký buổi rảnh.");
        var mentorId = user.Role == Role.Admin ? request.MentorId ?? 0 : user.Id;
        var mentor = await uow.UserAccounts.GetByIdAsync(mentorId, ct);
        if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor) return Fail("Mentor không hợp lệ.");
        var thisMonday = MentorScheduleRules.Monday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)));
        if (request.WeekStart.DayOfWeek != DayOfWeek.Monday ||
            (user.Role == Role.Mentor && !MentorScheduleRules.CanRegister(request.WeekStart, DateTime.UtcNow)) ||
            (user.Role == Role.Admin && (request.WeekStart < thisMonday || request.WeekStart > thisMonday.AddDays(7))))
            return Fail("Chỉ đăng ký tuần sau, trước 18:00 thứ Sáu theo giờ Việt Nam.");
        if (request.Periods is null || request.Periods.Count < 3 || request.Periods.Count > 21)
            return Fail("Đăng ký tối thiểu 3 buổi/tuần, chọn buổi sáng, chiều hoặc tối.");
        if (request.Periods.Distinct().Count() != request.Periods.Count || request.Periods.Any(x =>
            x.Date < request.WeekStart || x.Date >= request.WeekStart.AddDays(7) || x.Period is not ("Morning" or "Afternoon" or "Evening")))
            return Fail("Chỉ được đăng ký sáng hoặc chiều trong tuần đã chọn; không được trùng buổi.");
        var old = await uow.MentorWeeklyAvailabilities.Query().Include(x => x.Slots)
            .Where(x => x.MentorId == mentorId && !x.IsDeleted && x.Date >= request.WeekStart && x.Date < request.WeekStart.AddDays(7)).ToListAsync(ct);
        var removed = old.Where(x => !request.Periods.Contains(new WeeklyPeriodRequest(x.Date, x.Period))).ToList();
        if (removed.Any(x => x.Slots.Any(s => !s.IsDeleted))) return Fail("Buổi đã được xếp tiết; hãy gửi yêu cầu điều chỉnh cho admin.");
        foreach (var row in removed) { row.IsDeleted = true; row.ModifiedDate = DateTime.UtcNow; }
        foreach (var period in request.Periods.Where(p => !old.Any(x => x.Date == p.Date && x.Period == p.Period)))
            await uow.MentorWeeklyAvailabilities.AddAsync(new MentorWeeklyAvailability { MentorId = mentorId, Date = period.Date, Period = period.Period }, ct);
        await uow.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> AssignAsync(AssignMentorSlotsRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return Fail("Chỉ admin được xếp tiết.");
        if (request.SlotIndexes is null || request.SlotIndexes.Count == 0 || request.SlotIndexes.Any(x => x is < 0 or > 3)
            || request.SlotIndexes.Distinct().Count() != request.SlotIndexes.Count) return Fail("Chọn tiết từ 0 đến 3, không trùng.");
        var period = await uow.MentorWeeklyAvailabilities.Query().Include(x => x.Mentor).FirstOrDefaultAsync(x => x.Id == request.WeeklyAvailabilityId && !x.IsDeleted, ct);
        if (period is null || period.Mentor.IsDeleted || period.Mentor.Role != Role.Mentor) return Fail("Không tìm thấy đăng ký rảnh hợp lệ.");
        var starts = request.SlotIndexes.Select(i => MentorScheduleRules.SlotStart(period.Date, period.Period, i)).ToList();
        if (starts.Any(t => t <= DateTime.UtcNow)) return Fail("Không thể xếp tiết đã bắt đầu.");
        foreach (var start in starts)
            if (await uow.MentorAvailabilities.HasOverlappingSlotAsync(period.MentorId, start, start.AddMinutes(50), ct))
                return Fail("Tiết bị trùng với lịch của mentor. Hãy tải lại lịch.");
        foreach (var start in starts)
            await uow.MentorAvailabilities.AddAsync(new MentorAvailability {
                MentorId = period.MentorId, MentorWeeklyAvailabilityId = period.Id, StartTime = start,
                EndTime = start.AddMinutes(50), MaxLearners = 1, IsApproved = false }, ct);
        // Optimistic lock on the parent serializes concurrent administrators assigning the same period.
        period.ModifiedDate = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> PublishAsync(DateOnly weekStart, CancellationToken ct)
    {
        if (!IsAdmin) return Fail("Chỉ admin được công bố lịch.");
        if (weekStart.DayOfWeek != DayOfWeek.Monday) return Fail("Tuần phải bắt đầu thứ Hai.");
        var from = MentorScheduleRules.SlotStart(weekStart, "Morning", 0).AddHours(-8);
        if (from.AddDays(7) <= DateTime.UtcNow) return Fail("Tuần đã kết thúc.");
        var registrations = await uow.MentorWeeklyAvailabilities.Query().Include(x => x.Mentor)
            .Where(x => !x.IsDeleted && !x.Mentor.IsDeleted && x.Date >= weekStart && x.Date < weekStart.AddDays(7)).ToListAsync(ct);
        if (registrations.Count == 0) return Fail("Chưa có mentor đăng ký trong tuần.");
        var slots = await uow.MentorAvailabilities.Query().Where(x => !x.IsDeleted && x.RejectedAt == null
            && x.MentorWeeklyAvailabilityId != null && x.StartTime >= from && x.StartTime < from.AddDays(7)).ToListAsync(ct);
        var mentors = registrations.Select(x => x.Mentor).DistinctBy(x => x.Id).ToList();
        var missing = mentors.Where(m => slots.Count(s => s.MentorId == m.Id) < 5).ToList();
        if (missing.Count > 0) return Fail("Chưa đủ 5 tiết: " + string.Join(", ", missing.Select(m => $"{m.FirstName} {m.LastName}")));
        var drafts = slots.Where(x => !x.IsApproved && x.StartTime > DateTime.UtcNow).ToList();
        if (drafts.Count == 0) return Fail("Không có tiết nháp mới để công bố.");
        foreach (var slot in drafts) { slot.IsApproved = true; slot.PublishedAt = DateTime.UtcNow; slot.AdminName = $"Admin #{claims.GetUserClaim().Id}"; }
        foreach (var mentor in mentors.Where(m => drafts.Any(s => s.MentorId == m.Id)))
            await uow.Notifications.AddAsync(new Notification { UserId = mentor.Id, Title = "Lịch tư vấn tuần đã công bố",
                Message = $"Lịch tuần {weekStart:dd/MM/yyyy} đã sẵn sàng. Vui lòng xem lịch riêng.", Type = "MentorSchedule", LinkUrl = "/mentor-availability/" }, ct);
        await uow.SaveChangesAsync(ct);
        var failures = new List<string>();
        foreach (var mentor in mentors.Where(m => drafts.Any(s => s.MentorId == m.Id)))
        {
            var lines = slots.Where(x => x.MentorId == mentor.Id).OrderBy(x => x.StartTime)
                .Select(x => $"{x.StartTime.AddHours(7):ddd dd/MM HH:mm}–{x.EndTime.AddHours(7):HH:mm} (giờ Việt Nam)");
            try {
                var sent = await email.SendEmailAsync(mentor.Email!, $"Lịch tư vấn tuần {weekStart:dd/MM/yyyy} · RikiPath", "<h2>Lịch tư vấn tuần</h2><p>" + string.Join("<br/>", lines) + "</p>", ct);
                if (!sent.IsSuccess) failures.Add($"Mentor #{mentor.Id}");
            } catch (Exception ex) { logger.LogWarning(ex, "Schedule email failed for {MentorId}", mentor.Id); failures.Add($"Mentor #{mentor.Id}"); }
        }
        return failures.Count == 0 ? ApiResponse<bool>.Success(true) : ApiResponse<bool>.Fail(
            "Lịch đã công bố và đã gửi thông báo hệ thống; email chưa gửi được cho " + string.Join(", ", failures), HttpStatusCode.BadGateway);
    }

    public async Task<ApiResponse<bool>> RequestChangeAsync(int slotId, string reason, CancellationToken ct)
    {
        var user = claims.GetUserClaim();
        var slot = await uow.MentorAvailabilities.GetByIdAsync(slotId, ct);
        if (user.Role != Role.Mentor || slot is null || slot.IsDeleted || slot.MentorId != user.Id) return Fail("Không có quyền yêu cầu thay đổi tiết này.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) return Fail("Nhập lý do từ 1 đến 1000 ký tự.");
        if (slot.StartTime <= DateTime.UtcNow || slot.ChangeRequestedAt != null) return Fail("Tiết đã bắt đầu hoặc đã có yêu cầu chờ xử lý.");
        slot.ChangeRequestReason = reason.Trim(); slot.ChangeRequestedAt = DateTime.UtcNow; slot.ChangeResolution = null;
        var admins = await uow.UserAccounts.FindAsync(x => !x.IsDeleted && x.Role == Role.Admin, ct);
        foreach (var admin in admins)
            await uow.Notifications.AddAsync(new Notification { UserId = admin.Id,
                Title = "Mentor yêu cầu điều chỉnh lịch", Message = $"Mentor #{user.Id}, tiết #{slot.Id}: {reason.Trim()}",
                Type = "MentorSchedule", LinkUrl = "/admin/mentor-schedules/" }, ct);
        await uow.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> ResolveChangeAsync(int slotId, ResolveMentorSlotRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return Fail("Chỉ admin được điều chỉnh lịch.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) return Fail("Nhập lý do xử lý (tối đa 1000 ký tự).");
        var slot = await uow.MentorAvailabilities.Query().Include(x => x.Mentor).Include(x => x.MentorBookings).ThenInclude(x => x.UserSubscription).ThenInclude(x => x.UserAccount)
            .FirstOrDefaultAsync(x => x.Id == slotId && !x.IsDeleted, ct);
        if (slot is null || slot.StartTime <= DateTime.UtcNow) return Fail("Không tìm thấy tiết tương lai.");
        if (request.CancelSlot && slot.MentorBookings.Any(b => b.Status == MentorStatus.AwaitingPayment
            && b.UserSubscription.PaymentStatus == PaymentStatus.Pending && b.UserSubscription.PaymentExpiresAt > DateTime.UtcNow))
            return Fail("Tiết đang được giữ trong lúc thanh toán. Chờ tối đa 15 phút rồi kiểm tra lại trước khi hủy.");
        var affectedLearners = request.CancelSlot ? slot.MentorBookings
            .Where(b => !b.IsDeleted && b.Status is MentorStatus.Assigned or MentorStatus.Accepted)
            .Select(b => b.UserSubscription.UserAccount).DistinctBy(x => x.Id).ToList() : [];
        await uow.ExecuteInTransactionAsync(async token => {
            if (request.CancelSlot) {
                foreach (var booking in slot.MentorBookings.Where(b => !b.IsDeleted && b.Status is MentorStatus.Assigned or MentorStatus.Accepted or MentorStatus.AwaitingPayment)) {
                    Refund(booking, true); booking.Status = MentorStatus.Cancelled; booking.MeetingLink = null;
                    if (booking.UserSubscription.PaymentStatus == PaymentStatus.Pending) {
                        booking.UserSubscription.PaymentStatus = PaymentStatus.Failed;
                        booking.UserSubscription.PaymentExpiresAt = null;
                    }
                    await uow.Notifications.AddAsync(new Notification { UserId = booking.UserSubscription.UserId,
                        Title = "Lịch tư vấn đã hủy", Message = request.Reason.Trim() + ". Lượt đã được hoàn; vui lòng chọn lịch mới.", Type = "MentorSchedule", LinkUrl = "/consultation-center/" }, token);
                }
                slot.BookedCount = 0; slot.IsDeleted = true;
            }
            slot.ChangeResolution = request.Reason.Trim(); slot.ChangeRequestedAt = null;
            await uow.Notifications.AddAsync(new Notification { UserId = slot.MentorId, Title = "Lịch tư vấn đã được xử lý",
                Message = request.Reason.Trim(), Type = "MentorSchedule", LinkUrl = "/mentor-availability/" }, token);
            await uow.SaveChangesAsync(token);
        }, ct);
        foreach (var recipient in affectedLearners.Append(slot.Mentor).DistinctBy(x => x.Id)) {
            try { await email.SendEmailAsync(recipient.Email!, "Điều chỉnh lịch tư vấn · RikiPath", "<h2>Điều chỉnh lịch tư vấn</h2><p>" + System.Net.WebUtility.HtmlEncode(request.Reason) + "</p>", ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Change resolution email failed for slot {SlotId}", slotId); }
        }
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> CancelBookingAsync(int bookingId, CancellationToken ct)
    {
        var user = claims.GetUserClaim();
        var booking = await uow.MentorBookings.Query().Include(x => x.UserSubscription).Include(x => x.MentorAvailability)
            .FirstOrDefaultAsync(x => x.Id == bookingId && !x.IsDeleted, ct);
        if (user.Role != Role.Learner || booking is null || booking.UserSubscription.UserId != user.Id) return Fail("Không tìm thấy booking của bạn.");
        if (booking.Status == MentorStatus.Cancelled) return ApiResponse<bool>.Success(true);
        if (booking.Status is not (MentorStatus.Assigned or MentorStatus.Accepted) || booking.UserSubscription.PaymentStatus != PaymentStatus.Paid)
            return Fail("Booking không ở trạng thái có thể hủy.");
        if (booking.MentorAvailability is not {} slot || slot.StartTime - DateTime.UtcNow < TimeSpan.FromHours(24))
            return Fail("Chỉ được hủy trước giờ bắt đầu ít nhất 24 tiếng.");
        await uow.ExecuteInTransactionAsync(async token => {
            Refund(booking, false); booking.Status = MentorStatus.Cancelled; booking.MeetingLink = null;
            slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
            // A private room must not retain a cancelled learner's room link when this slot is booked again.
            if (slot.MaxLearners == 1) slot.RoomId = Guid.NewGuid();
            await uow.SaveChangesAsync(token);
        }, ct);
        return ApiResponse<bool>.Success(true);
    }

    private static void Refund(MentorBooking booking, bool adminCancelled)
    {
        var sub = booking.UserSubscription;
        sub.MeetingSessionsUsed = Math.Max(0, sub.MeetingSessionsUsed - 1);
        if (adminCancelled && sub.PaymentStatus == PaymentStatus.Paid && (sub.EndDate is null || sub.EndDate < DateTime.UtcNow.AddDays(7)))
            sub.EndDate = DateTime.UtcNow.AddDays(7);
    }
}
