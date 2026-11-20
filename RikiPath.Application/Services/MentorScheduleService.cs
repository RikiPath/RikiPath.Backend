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
                Title = "Cố vấn yêu cầu điều chỉnh lịch", Message = $"Cố vấn đề nghị điều chỉnh một khung giờ: {reason.Trim()}",
                Type = "MentorSchedule", LinkUrl = "/admin/mentor-schedules/" }, ct);
        await uow.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> ResolveChangeAsync(int slotId, ResolveMentorSlotRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return Fail("Chỉ admin được điều chỉnh lịch.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) return Fail("Nhập lý do xử lý (tối đa 1000 ký tự).");
        if (request.CancelSlot) return ApiResponse<bool>.Fail(
            "Không thể hủy và hoàn vé qua lịch tuần cũ. Hãy xử lý từng booking qua /api/mentor-bookings/{id}/cancel, sau đó duyệt hủy khung giờ tại /api/admin/mentor-availabilities/review-cancel.",
            HttpStatusCode.Conflict);
        var slot = await uow.MentorAvailabilities.Query().Include(x => x.Mentor)
            .FirstOrDefaultAsync(x => x.Id == slotId && !x.IsDeleted, ct);
        if (slot is null || slot.StartTime <= DateTime.UtcNow) return Fail("Không tìm thấy tiết tương lai.");
        await uow.ExecuteInTransactionAsync(async token => {
            slot.ChangeResolution = request.Reason.Trim(); slot.ChangeRequestedAt = null;
            await uow.Notifications.AddAsync(new Notification { UserId = slot.MentorId, Title = "Lịch tư vấn đã được xử lý",
                Message = request.Reason.Trim(), Type = "MentorSchedule", LinkUrl = "/mentor-availability/" }, token);
            await uow.SaveChangesAsync(token);
        }, ct);
        try { await email.SendEmailAsync(slot.Mentor.Email!, "Điều chỉnh lịch tư vấn · RikiPath", "<h2>Điều chỉnh lịch tư vấn</h2><p>" + System.Net.WebUtility.HtmlEncode(request.Reason) + "</p>", ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Change resolution email failed for slot {SlotId}", slotId); }
        return ApiResponse<bool>.Success(true);
    }
}
