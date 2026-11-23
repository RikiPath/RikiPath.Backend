using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.EmailTemplates;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorScheduleService(IUnitOfWork uow, IClaimService claims, IEmailService email,
    ILogger<MentorScheduleService> logger, IEmailTemplateService? emailTemplates = null,
    AppSettings? appSettings = null) : IMentorScheduleService
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
        if (!IsAdmin)
            return ApiResponse<bool>.Fail("Chỉ Admin mới có quyền điều chỉnh lịch.", HttpStatusCode.Forbidden);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Fail("Nhập lý do xử lý (tối đa 1000 ký tự).");
        if (request.CancelSlot)
            return ApiResponse<bool>.Fail(
                "Không thể hủy trực tiếp tại đây. Hãy duyệt yêu cầu hủy khung giờ tại màn hình Duyệt đổi/hủy lịch hoặc hủy lớp qua quy trình ngoại lệ.",
                HttpStatusCode.Conflict);
        var slot = await uow.MentorAvailabilities.Query().Include(x => x.Mentor)
            .FirstOrDefaultAsync(x => x.Id == slotId && !x.IsDeleted, ct);
        if (slot is null || slot.StartTime <= DateTime.UtcNow)
            return ApiResponse<bool>.Fail("Không tìm thấy khung giờ tương lai.", HttpStatusCode.NotFound);
        var now = DateTime.UtcNow;
        var reason = request.Reason.Trim();
        await uow.ExecuteInTransactionAsync(async token => {
            slot.ChangeResolution = reason;
            slot.ChangeRequestedAt = null;
            slot.ModifiedDate = now;
            await uow.Notifications.AddAsync(new Notification { UserId = slot.MentorId, Title = "Lịch dạy đã được xử lý",
                Message = reason, Type = "MentorSchedule", LinkUrl = "/mentor-shift-schedule/" }, token);
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = claims.GetUserClaim().Id,
                EntityType = "MentorAvailability",
                EntityId = slot.Id,
                Action = "ChangeResolved",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { SlotId = slot.Id, slot.MentorId, ChangeResolution = reason }),
                Reason = reason,
                CreatedAtUtc = now,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);
        if (slot.Mentor is not null && NotificationPreferenceHelper.ShouldDeliverEmail(slot.Mentor, "MentorSchedule"))
        {
            try
            {
                var templates = emailTemplates ?? new EmailTemplateService(appSettings);
                var mentorName = UserNameHelper.FormatFullName(slot.Mentor.FirstName, slot.Mentor.LastName);
                var slotRange = EmailTemplateService.FormatVietnamSlotRange(slot.StartTime, slot.EndTime);
                var rendered = templates.BuildScheduleNotification(new ScheduleNotificationEmailData(
                    mentorName,
                    "Điều chỉnh lịch dạy · RikiPath",
                    "Điều chỉnh lịch dạy",
                    "Yêu cầu điều chỉnh lịch dạy đã được xử lý",
                    "Quản trị viên RikiPath vừa phản hồi yêu cầu điều chỉnh khung giờ giảng dạy của bạn.",
                    [
                        new("Khung giờ", slotRange),
                        new("Mã khung giờ", $"#{slot.Id}"),
                        new("Trạng thái", "Đã xử lý bởi quản trị viên"),
                    ],
                    SakuraEmailTone.Info,
                    "Nội dung phản hồi từ quản trị viên",
                    reason,
                    "Xem lịch dạy của tôi",
                    "/mentor-shift-schedule/"));
                await email.SendEmailAsync(slot.Mentor.Email!, rendered, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Change resolution email failed for slot {SlotId}", slotId); }
        }
        return ApiResponse<bool>.Success(true);
    }
}
