using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorAvailabilityReviewService(IUnitOfWork unitOfWork, IClaimService claimService)
    : IMentorAvailabilityReviewService
{
    private const int MinAdvanceMonths = 1;
    private const int MinCancelAdvanceDays = 7;
    private const int MaxBulkSlots = 50;

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
                || await unitOfWork.StudyCourses.AnyAsync(c => c.Sessions.Any(s => s.AvailabilityId == id), token))
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
            .Where(s => s.MentorId == mentorId && s.StartTime >= from && s.StartTime < to)
            .OrderBy(s => s.StartTime).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var slotIds = slots.Select(s => s.Id).ToArray();
        var cohortIds = slotIds.Length == 0
            ? new HashSet<int>()
            : (await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .SelectMany(c => c.Sessions.Where(s => slotIds.Contains(s.AvailabilityId)).Select(s => s.AvailabilityId))
                .ToListAsync(ct)).ToHashSet();
        return ApiResponse<List<AdminMentorScheduleResponse>>.Success(slots.Select(s => {
            var booked = s.BookedCount > 0 || cohortIds.Contains(s.Id);
            var status = s.IsDeleted ? "Cancelled" : s.RejectedAt != null ? "Rejected" : !s.IsApproved ? "Draft" :
                s.EndTime <= now ? "Ended" : booked ? "Booked" : "Available";
            return new AdminMentorScheduleResponse(s.Id, s.StartTime, s.EndTime, status, []);
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

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ cố vấn mới có thể xem lịch tư vấn của mình.", HttpStatusCode.Forbidden);
        var slots = await unitOfWork.MentorAvailabilities.GetByMentorAsync(claims.Id);
        var slotIds = slots.Select(x => x.Id).ToList();
        var cohortMap = slotIds.Count == 0
            ? new Dictionary<int, int>()
            : (await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .SelectMany(c => c.Sessions.Where(s => slotIds.Contains(s.AvailabilityId)).Select(s => new { s.AvailabilityId, CohortId = c.Id }))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.AvailabilityId)
                .ToDictionary(g => g.Key, g => g.First().CohortId);
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim(), cohortMap.TryGetValue(s.Id, out var cid) ? cid : null))
            .ToList());
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(
        CreateMentorAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var actor = claimService.GetUserClaim();
        if (actor.Role != Role.Mentor) return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Mentor được đăng ký lịch rảnh.", HttpStatusCode.Forbidden);
        if (request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc
            || request.StartTime <= DateTime.UtcNow || request.EndTime <= request.StartTime
            || request.EndTime - request.StartTime > TimeSpan.FromHours(8)
            || request.StartTime.AddHours(7).Date != request.EndTime.AddTicks(-1).AddHours(7).Date)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chọn giờ rảnh trong tương lai, cùng ngày, tối đa 8 giờ và có múi giờ UTC.");
        var startDate = DateOnly.FromDateTime(request.StartTime.AddHours(7));
        var until = request.RepeatUntil ?? startDate;
        var days = request.Weekdays;
        if (until < startDate || until > startDate.AddDays(366)
            || (request.RepeatUntil.HasValue && (days is null || days.Count == 0 || days.Any(x => !Enum.IsDefined(x)))))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chọn ngày lặp và hạn kết thúc tối đa một năm.");
        var rows = new List<MentorAvailability>();
        for (var day = startDate; day <= until; day = day.AddDays(1))
            if (!request.RepeatUntil.HasValue || days!.Contains(day.DayOfWeek))
            {
                var delta = day.DayNumber - startDate.DayNumber;
                rows.Add(new() { MentorId = actor.Id, StartTime = request.StartTime.AddDays(delta),
                    EndTime = request.EndTime.AddDays(delta), MaxLearners = 1, IsApproved = false });
            }
        if (rows.Count == 0 || rows.Count > 500) return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không có ngày phù hợp hoặc quá nhiều buổi.");
        try { return await CreateRows(); }
        catch (AvailabilityOverlapException) { return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có ngày/giờ trùng lịch; chưa lưu đăng ký nào.", HttpStatusCode.Conflict); }
        async Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateRows()
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(actor.Id, actor.Id, token);
                foreach (var slot in rows)
                    if (await unitOfWork.MentorAvailabilities.HasOverlappingSlotAsync(actor.Id, slot.StartTime, slot.EndTime, token))
                        throw new AvailabilityOverlapException();
                await unitOfWork.MentorAvailabilities.AddRangeAsync(rows, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);
            return ApiResponse<List<MentorAvailabilityResponse>>.Success(rows.Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim())).ToList());
        }
    }
    private sealed class AvailabilityOverlapException : Exception;

    public async Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
        if (slot == null || slot.IsDeleted)
            return ApiResponse<bool>.Fail("Không tìm thấy khung giờ.", HttpStatusCode.NotFound);
        if (slot.MentorId != claims.Id && claims.Role != Role.Admin)
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa khung giờ này.", HttpStatusCode.Forbidden);
        if (slot.IsApproved || slot.BookedCount > 0)
            return ApiResponse<bool>.Fail("Lịch đã duyệt phải gửi yêu cầu điều chỉnh/hủy để Admin xem xét.", HttpStatusCode.Conflict);

        slot.IsDeleted = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> RequestCancelAvailabilitiesAsync(
        CancelAvailabilitiesRequest request, CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(request.Ids);
        if (ids.Count == 0 || ids.Count > MaxBulkSlots || ids.Any(x => x <= 0))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail($"Danh sách khung giờ không hợp lệ (cần từ 1 đến {MaxBulkSlots} khung giờ).");
        if (request.Reason?.Length > 1000)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Lý do hủy tối đa 1000 ký tự.");

        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ cố vấn mới có thể hủy lịch tư vấn của mình.", HttpStatusCode.Forbidden);

        var slots = await unitOfWork.MentorAvailabilities.Query()
            .Include(x => x.Mentor)
            .Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.MentorId == claims.Id)
            .ToListAsync(cancellationToken);
        if (slots.Count != ids.Count)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound);

        if (await unitOfWork.StudyCohorts.AnyAsync(c => c.Sessions.Any(s => ids.Contains(s.AvailabilityId)), cancellationToken))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Lịch thuộc lớp nhóm; gửi yêu cầu trong hồ sơ khóa học để xử lý đúng phạm vi.", HttpStatusCode.Conflict);
        var now = DateTime.UtcNow;
        if (slots.Any(x => x.CancelRequestedAt is not null))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có khung giờ đang chờ quản trị viên duyệt hủy.", HttpStatusCode.Conflict);
        if (slots.Any(x => x.StartTime - now < TimeSpan.FromDays(MinCancelAdvanceDays)))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail(
                $"Chỉ được yêu cầu hủy trước giờ bắt đầu ít nhất {MinCancelAdvanceDays} ngày.", HttpStatusCode.Conflict);
        if (slots.Any(x => x.BookedCount > 0))
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có khung giờ đã được đặt nên không thể hủy.", HttpStatusCode.Conflict);

        foreach (var slot in slots)
        {
            slot.CancelRequestedAt = now;
            slot.CancelReason = request.Reason?.Trim();
            slot.CancelRejectedAt = null;
            slot.CancelRejectionReason = null;
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots.OrderBy(x => x.StartTime)
            .Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim())).ToList());
    }

    public async Task<ApiResponse<bool>> ReviewCancelAvailabilitiesAsync(
        List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default)
    {
        var distinctIds = NormalizeIds(ids);
        if (distinctIds.Count == 0 || distinctIds.Count > MaxBulkSlots || distinctIds.Any(x => x <= 0))
            return ApiResponse<bool>.Fail($"Danh sách khung giờ không hợp lệ (cần từ 1 đến {MaxBulkSlots} khung giờ).");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt yêu cầu hủy.", HttpStatusCode.Forbidden);

        var slots = await unitOfWork.MentorAvailabilities.Query()
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        if (slots.Count != distinctIds.Count)
            return ApiResponse<bool>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound);
        if (slots.Any(x => x.CancelRequestedAt is null))
            return ApiResponse<bool>.Fail("Có khung giờ chưa có yêu cầu hủy.", HttpStatusCode.Conflict);

        var now = DateTime.UtcNow;
        if (approve && (slots.Any(x => x.BookedCount > 0)
            || await unitOfWork.StudyCohorts.AnyAsync(c => c.Sessions.Any(s => distinctIds.Contains(s.AvailabilityId)), cancellationToken)))
            return ApiResponse<bool>.Fail("Có khung giờ đã được đặt nên không thể duyệt hủy.", HttpStatusCode.Conflict);

        foreach (var slot in slots)
        {
            if (approve)
            {
                slot.IsDeleted = true;
            }
            else
            {
                slot.CancelRequestedAt = null;
                slot.CancelRejectedAt = now;
                slot.CancelRejectionReason = rejectionReason?.Trim();
            }
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetPendingAvailabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Admin mới xem được danh sách chờ duyệt.", HttpStatusCode.Forbidden);

        var slots = await unitOfWork.MentorAvailabilities.Query()
            .Include(x => x.Mentor)
            .Where(x => !x.IsDeleted && !x.IsApproved && x.RejectedAt == null && x.StartTime > DateTime.UtcNow)
            .OrderBy(x => x.MentorId).ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim())).ToList());
    }

    public async Task<ApiResponse<bool>> ReviewAvailabilitiesAsync(
        List<int> ids, bool approve, string? rejectionReason, CancellationToken cancellationToken = default)
    {
        var distinctIds = NormalizeIds(ids);
        if (distinctIds.Count == 0 || distinctIds.Count > MaxBulkSlots || distinctIds.Any(x => x <= 0))
            return ApiResponse<bool>.Fail($"Danh sách khung giờ không hợp lệ (cần từ 1 đến {MaxBulkSlots} khung giờ).");
        if (!approve && string.IsNullOrWhiteSpace(rejectionReason))
            return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối.");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt lịch.", HttpStatusCode.Forbidden);

        var user = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
        var adminName = user is not null ? $"{user.FirstName} {user.LastName}".Trim() : null;

        var slots = await unitOfWork.MentorAvailabilities.Query()
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (slots.Count != distinctIds.Count)
            return ApiResponse<bool>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound);
        if (slots.Any(x => x.IsApproved || x.RejectedAt is not null))
            return ApiResponse<bool>.Fail("Có lịch đã được xử lý.", HttpStatusCode.Conflict);
        if (slots.Any(x => x.StartTime <= now))
            return ApiResponse<bool>.Fail("Có lịch đã quá hạn, không thể duyệt.", HttpStatusCode.Conflict);

        if (approve)
        {
            foreach (var slot in slots)
            {
                var batchOverlap = slots.Any(other => other.Id != slot.Id && other.MentorId == slot.MentorId
                    && other.StartTime < slot.EndTime && slot.StartTime < other.EndTime);
                var existingOverlap = await unitOfWork.MentorAvailabilities.Query().AnyAsync(x =>
                    !distinctIds.Contains(x.Id) && x.MentorId == slot.MentorId && !x.IsDeleted
                    && x.IsApproved && x.RejectedAt == null
                    && x.StartTime < slot.EndTime && slot.StartTime < x.EndTime, cancellationToken);
                if (batchOverlap || existingOverlap)
                    return ApiResponse<bool>.Fail("Có khung giờ bị trùng với lịch đã duyệt khác của cố vấn.", HttpStatusCode.Conflict);
            }
        }

        foreach (var slot in slots)
        {
            slot.IsApproved = approve;
            slot.PublishedAt = approve ? now : null;
            slot.RejectedAt = approve ? null : now;
            slot.RejectionReason = approve ? null : rejectionReason!.Trim();
            slot.AdminName = adminName;
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Success(true);
    }

    private static List<int> NormalizeIds(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    private static MentorAvailabilityResponse Map(MentorAvailability slot, string mentorName, int? cohortId = null) => new()
    {
        Id = slot.Id, MentorId = slot.MentorId, MentorName = mentorName,
        StartTime = slot.StartTime, EndTime = slot.EndTime, IsApproved = slot.IsApproved,
        RejectedAt = slot.RejectedAt, RejectionReason = slot.RejectionReason,
        IsBooked = slot.IsFull, BookedCount = slot.BookedCount, MaxLearners = slot.MaxLearners,
        AdminName = slot.AdminName,
        AdvanceDaysAtCreation = slot.CreatedDate is DateTime createdAt ? (int)(slot.StartTime - createdAt).TotalDays : 0,
        MeetsAdvanceRule = slot.CreatedDate is DateTime created && slot.StartTime >= created.AddMonths(MinAdvanceMonths),
        CancelRequestedAt = slot.CancelRequestedAt, CancelReason = slot.CancelReason,
        CancelRejectedAt = slot.CancelRejectedAt, CancelRejectionReason = slot.CancelRejectionReason,
        MentorWeeklyAvailabilityId = slot.MentorWeeklyAvailabilityId, PublishedAt = slot.PublishedAt,
        ChangeRequestReason = slot.ChangeRequestReason, ChangeRequestedAt = slot.ChangeRequestedAt,
        ChangeResolution = slot.ChangeResolution,
        IsInCohort = cohortId.HasValue, CohortId = cohortId
    };
}
