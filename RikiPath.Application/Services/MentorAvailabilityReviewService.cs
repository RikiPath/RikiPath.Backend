using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RikiPath.Application.IServices;
using RikiPath.Application.Options;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorAvailabilityReviewService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IMentorGroupPolicyService? groupPolicies = null,
    IOptions<MentorSlotGenerationOptions>? slotOptions = null)
    : IMentorAvailabilityReviewService
{
    private const int MinAdvanceMonths = 1;
    private const int MinCancelAdvanceDays = 7;
    private const int MaxBulkSlots = 50;
    private static readonly ConcurrentDictionary<string, (DateTime ExpiresUtc, List<int> SlotIds)> IdempotencyRecords = new(StringComparer.Ordinal);

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
        var cohortMap = await LoadCohortSlotContextsAsync(slotIds, DateTime.UtcNow, cancellationToken);
        var mentors = await unitOfWork.UserAccounts.FindAsync(x => x.Role == Role.Mentor, cancellationToken);
        var names = mentors.ToDictionary(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim());
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .OrderBy(x => x.IsApproved || x.RejectedAt != null).ThenBy(x => x.StartTime)
            .Select(x => Map(x, names.GetValueOrDefault(x.MentorId) ?? "Cố vấn chưa cập nhật tên",
                cohortMap.GetValueOrDefault(x.Id))).ToList());
    }

    public async Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ quản trị viên mới được duyệt lịch dạy của Giáo viên.", HttpStatusCode.Forbidden);
        var normalizedReason = reason?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(normalizedReason))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Vui lòng nhập lý do từ chối.");
        if (normalizedReason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do tối đa 1000 ký tự.");

        var seed = await unitOfWork.MentorAvailabilities.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (seed is null)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy lịch dạy của Giáo viên.", HttpStatusCode.NotFound);

        ApiResponse<MentorAvailabilityResponse>? errorResponse = null;
        MentorAvailabilityResponse? mapped = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.StudyCourses.LockScheduleAsync(seed.MentorId, seed.MentorId, token);
            var slot = await unitOfWork.MentorAvailabilities.Query().Include(x => x.GroupPolicy).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, token);
            if (slot is null) { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy lịch dạy của Giáo viên.", HttpStatusCode.NotFound); return; }
            if (slot.IsApproved || slot.RejectedAt != null) { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Lịch đã được xử lý. Vui lòng tải lại danh sách.", HttpStatusCode.Conflict); return; }
            if (slot.StartTime <= DateTime.UtcNow) { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Không thể duyệt lịch đã bắt đầu hoặc hết hạn.", HttpStatusCode.Conflict); return; }
            if (slot.BookedCount > 0) { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Không thể thay đổi duyệt lịch đã có học viên đặt.", HttpStatusCode.Conflict); return; }

            var mentor = await unitOfWork.UserAccounts.Query().Include(u => u.MentorProfile).FirstOrDefaultAsync(u => u.Id == slot.MentorId, token);
            if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor)
            { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Giáo viên này không còn hoạt động.", HttpStatusCode.Conflict); return; }

            if (approve)
            {
                if (mentor.MentorProfile?.VerificationStatus != MentorVerificationStatus.Approved)
                { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ được duyệt lịch cho Giáo viên đã phê duyệt hồ sơ.", HttpStatusCode.Conflict); return; }

                var policyErr = await ValidateSlotPolicyCapsAsync(slot, [], token);
                if (policyErr is not null)
                { errorResponse = ApiResponse<MentorAvailabilityResponse>.Fail(policyErr, HttpStatusCode.Conflict); return; }
            }

            var admin = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, token);
            var now = DateTime.UtcNow;
            slot.IsApproved = approve;
            slot.PublishedAt = approve ? now : null;
            slot.AdminName = admin is null ? $"Admin #{claims.Id}" : $"{admin.FirstName} {admin.LastName}".Trim();
            slot.RejectedAt = approve ? null : now;
            slot.RejectionReason = approve ? null : normalizedReason;
            slot.ModifiedDate = now;
            unitOfWork.MentorAvailabilities.Update(slot);
            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = claims.Id,
                EntityType = "MentorAvailability",
                EntityId = slot.Id,
                Action = approve ? "Approved" : "Rejected",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { slot.Id, slot.MentorId, slot.StartTime, slot.EndTime, IsApproved = approve }),
                Reason = normalizedReason,
                CreatedAtUtc = now,
            }, token);
            await unitOfWork.SaveChangesAsync(token);
            mapped = Map(slot, $"{mentor.FirstName} {mentor.LastName}".Trim());
        }, cancellationToken);

        return errorResponse ?? ApiResponse<MentorAvailabilityResponse>.Success(mapped!);
    }

    private async Task<string?> ValidateSlotPolicyCapsAsync(
        MentorAvailability candidate,
        IReadOnlyCollection<MentorAvailability> batchCandidates,
        CancellationToken ct)
    {
        var policy = candidate.GroupPolicy ?? (groupPolicies is not null ? await groupPolicies.GetEffectiveAsync(candidate.StartTime, ct) : null);
        var restMinutes = Math.Max(0, policy?.RestMinutes ?? RikiPath.Domain.MentorScheduling.MentorSlotPolicy.MinimumRestMinutes);
        var dailyCap = policy?.DailySessionCap ?? RikiPath.Domain.MentorScheduling.MentorSlotPolicy.DefaultDailyBookingCap;
        var weeklyCap = policy?.WeeklySessionCap ?? 0;

        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(RikiPath.Domain.MentorScheduling.MentorSlotPolicy.BusinessTimeZoneId);
        var vnStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(candidate.StartTime, DateTimeKind.Utc), vnZone);
        var dayStartLocal = vnStart.Date;
        var diffToMon = ((int)dayStartLocal.DayOfWeek + 6) % 7;
        var weekStartLocal = dayStartLocal.AddDays(-diffToMon);
        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(weekStartLocal, vnZone);
        var weekEndUtc = weekStartUtc.AddDays(7);

        var dbApproved = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Where(x => x.MentorId == candidate.MentorId && !x.IsDeleted && x.IsApproved && x.RejectedAt == null
                && x.Id != candidate.Id
                && x.EndTime > weekStartUtc.AddMinutes(-restMinutes) && x.StartTime < weekEndUtc.AddMinutes(restMinutes))
            .Select(x => new { x.Id, x.StartTime, x.EndTime })
            .ToListAsync(ct);

        var peers = dbApproved.Select(x => (x.Id, x.StartTime, x.EndTime))
            .Concat(batchCandidates.Where(b => b.Id != candidate.Id && b.MentorId == candidate.MentorId)
                .Select(b => (b.Id, b.StartTime, b.EndTime)))
            .ToList();

        if (peers.Any(x => x.StartTime < candidate.EndTime && candidate.StartTime < x.EndTime))
            return "Giáo viên đã có khung giờ khác được duyệt trùng thời gian này.";

        if (restMinutes > 0 && peers.Any(x =>
                x.EndTime.AddMinutes(restMinutes) > candidate.StartTime && x.StartTime < candidate.EndTime.AddMinutes(restMinutes)))
            return $"Giáo viên cần ít nhất {restMinutes} phút nghỉ giữa hai khung giờ dạy theo chính sách.";

        var dayCount = 1 + peers.Count(x => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.StartTime, DateTimeKind.Utc), vnZone).Date == dayStartLocal);
        if (dailyCap > 0 && dayCount > dailyCap)
            return $"Vượt giới hạn tối đa {dailyCap} khung giờ/ngày của Giáo viên.";

        var weekCount = 1 + peers.Count(x => x.StartTime >= weekStartUtc && x.StartTime < weekEndUtc);
        if (weeklyCap > 0 && weekCount > weeklyCap)
            return $"Vượt giới hạn tối đa {weeklyCap} khung giờ/tuần của Giáo viên.";

        return null;
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetMyAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ cố vấn mới có thể xem lịch tư vấn của mình.", HttpStatusCode.Forbidden);
        var nowUtc = DateTime.UtcNow;
        var slots = await unitOfWork.MentorAvailabilities.GetByMentorAsync(claims.Id);
        var slotIds = slots.Select(x => x.Id).ToList();
        var cohortMap = await LoadCohortSlotContextsAsync(slotIds, nowUtc, cancellationToken);
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim(), cohortMap.GetValueOrDefault(s.Id), nowUtc))
            .ToList());
    }

    public async Task<ApiResponse<PagedMentorAvailabilityResponse>> QueryMyAvailabilityAsync(
        MentorAvailabilityQueryRequest request, CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Chỉ Giáo viên mới có thể tra cứu lịch dạy của mình.", HttpStatusCode.Forbidden);

        request ??= new MentorAvailabilityQueryRequest();
        if (request.Page <= 0 || request.PageSize is <= 0 or > 200)
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Tham số phân trang không hợp lệ (page >= 1, 1 <= pageSize <= 200).", HttpStatusCode.BadRequest);

        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value > request.ToDate.Value)
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Ngày bắt đầu không được lớn hơn ngày kết thúc.", HttpStatusCode.BadRequest);

        TimeOnly? filterStartClock = null;
        TimeOnly? filterEndClock = null;
        if (!string.IsNullOrWhiteSpace(request.StartTime))
        {
            if (!TimeOnly.TryParse(request.StartTime.Trim(), out var parsedStart))
                return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Định dạng giờ bắt đầu (startTime) không hợp lệ (HH:mm).", HttpStatusCode.BadRequest);
            filterStartClock = parsedStart;
        }
        if (!string.IsNullOrWhiteSpace(request.EndTime))
        {
            if (!TimeOnly.TryParse(request.EndTime.Trim(), out var parsedEnd))
                return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Định dạng giờ kết thúc (endTime) không hợp lệ (HH:mm).", HttpStatusCode.BadRequest);
            filterEndClock = parsedEnd;
        }
        if (filterStartClock.HasValue && filterEndClock.HasValue && filterStartClock.Value >= filterEndClock.Value)
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail("Giờ bắt đầu phải nhỏ hơn giờ kết thúc.", HttpStatusCode.BadRequest);

        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ALL", "Pending", "Approved", "Published", "Completed", "Rejected", "Cancelled", "Booked"
        };
        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? "ALL" : request.Status.Trim();
        if (!validStatuses.Contains(normalizedStatus))
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail($"Trạng thái lọc '{request.Status}' không hợp lệ.", HttpStatusCode.BadRequest);

        var validEventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ALL", "CohortSession", "Availability"
        };
        var normalizedEventType = string.IsNullOrWhiteSpace(request.EventType) ? "ALL" : request.EventType.Trim();
        if (!validEventTypes.Contains(normalizedEventType))
            return ApiResponse<PagedMentorAvailabilityResponse>.Fail($"Loại lịch '{request.EventType}' không hợp lệ.", HttpStatusCode.BadRequest);

        var query = unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Where(x => x.MentorId == claims.Id);

        if (!request.IncludeDeleted && !string.Equals(normalizedStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !x.IsDeleted);

        // Date filter in Vietnam time (UTC+7)
        if (request.FromDate.HasValue)
        {
            var fromUtc = request.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddHours(-7);
            query = query.Where(x => x.StartTime >= fromUtc);
        }
        if (request.ToDate.HasValue)
        {
            var toUtcExclusive = request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddHours(-7);
            query = query.Where(x => x.StartTime < toUtcExclusive);
        }

        var nowUtc = DateTime.UtcNow;
        var slots = await query.ToListAsync(cancellationToken);
        var slotIds = slots.Select(x => x.Id).ToList();
        var cohortMap = await LoadCohortSlotContextsAsync(slotIds, nowUtc, cancellationToken);

        var mapped = slots
            .Select(s => Map(s, string.Empty, cohortMap.GetValueOrDefault(s.Id), nowUtc))
            .AsEnumerable();

        // Filter by Status (keep 'Published' alias mapped cleanly to Approved for backwards compatibility)
        if (!string.Equals(normalizedStatus, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(normalizedStatus, "Published", StringComparison.OrdinalIgnoreCase))
            {
                mapped = mapped.Where(x => x.Status == "Approved" && x.PublishedAt.HasValue);
            }
            else
            {
                mapped = mapped.Where(x => string.Equals(x.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Filter by EventType (CohortSession vs Availability)
        if (!string.Equals(normalizedEventType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            mapped = mapped.Where(x => string.Equals(x.EventType, normalizedEventType, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by Time-of-Day interval overlap in GMT+7: [slotStartMin, slotEndMin) intersects [filterStartMin, filterEndMin)
        if (filterStartClock.HasValue || filterEndClock.HasValue)
        {
            var minM = filterStartClock.HasValue ? filterStartClock.Value.Hour * 60 + filterStartClock.Value.Minute : 0;
            var maxM = filterEndClock.HasValue ? filterEndClock.Value.Hour * 60 + filterEndClock.Value.Minute : 24 * 60;
            mapped = mapped.Where(x =>
            {
                var sLocal = x.StartTime.AddHours(7);
                var eLocal = x.EndTime.AddHours(7);
                var sMin = sLocal.Hour * 60 + sLocal.Minute;
                var eMin = eLocal.Hour * 60 + eLocal.Minute;
                if (eMin <= sMin) eMin = 24 * 60;
                return sMin < maxM && eMin > minM;
            });
        }

        // Smart Sorting
        var sortBy = (request.SortBy ?? "smart").Trim().ToLowerInvariant();
        var desc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<MentorAvailabilityResponse> ordered = sortBy switch
        {
            "status" => desc
                ? mapped.OrderByDescending(x => StatusPriority(x, nowUtc)).ThenBy(x => x.StartTime).ThenBy(x => x.Id)
                : mapped.OrderBy(x => StatusPriority(x, nowUtc)).ThenBy(x => x.StartTime).ThenBy(x => x.Id),
            "nearest" or "date" => desc
                ? mapped.OrderByDescending(x => x.StartTime).ThenByDescending(x => x.Id)
                : mapped.OrderBy(x => x.StartTime).ThenBy(x => x.Id),
            "starttime" or "time" => desc
                ? mapped.OrderByDescending(x => x.StartTime.AddHours(7).TimeOfDay).ThenBy(x => x.StartTime).ThenBy(x => x.Id)
                : mapped.OrderBy(x => x.StartTime.AddHours(7).TimeOfDay).ThenBy(x => x.StartTime).ThenBy(x => x.Id),
            "newest" => mapped.OrderByDescending(x => x.StartTime).ThenByDescending(x => x.Id),
            "oldest" => mapped.OrderBy(x => x.StartTime).ThenBy(x => x.Id),
            _ => mapped
                .OrderBy(x => SmartSortBucket(x, nowUtc))
                .ThenBy(x => SmartSortTimeKey(x, nowUtc))
                .ThenBy(x => x.Id)
        };

        var list = ordered.ToList();
        var total = list.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize));
        var pageItems = list
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return ApiResponse<PagedMentorAvailabilityResponse>.Success(new PagedMentorAvailabilityResponse
        {
            Items = pageItems,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        });
    }

    private static int StatusPriority(MentorAvailabilityResponse x, DateTime nowUtc) => x.Status switch
    {
        "Pending" => 1,
        "Approved" => 2,
        "Booked" => 3,
        "Completed" => 4,
        "Rejected" => 5,
        "Cancelled" => 6,
        _ => 3
    };

    private static int SmartSortBucket(MentorAvailabilityResponse x, DateTime nowUtc)
    {
        if (x.Status is "Rejected" or "Cancelled") return 4;
        if (x.Status == "Pending" && x.StartTime >= nowUtc) return 1;
        if (x.StartTime >= nowUtc) return 2;
        return 3;
    }

    private static long SmartSortTimeKey(MentorAvailabilityResponse x, DateTime nowUtc)
    {
        var bucket = SmartSortBucket(x, nowUtc);
        // Upcoming (buckets 1 & 2): nearest first; Past/Ended (buckets 3 & 4): most recent first
        return bucket is 1 or 2 ? x.StartTime.Ticks : -x.StartTime.Ticks;
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityAsync(
        CreateMentorAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var actor = claimService.GetUserClaim();
        if (actor.Role != Role.Mentor) return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Mentor được đăng ký lịch rảnh.", HttpStatusCode.Forbidden);
        if (request is null || request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc
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
        var items = new List<MentorAvailabilityBatchSlotItem>();
        for (var day = startDate; day <= until; day = day.AddDays(1))
            if (!request.RepeatUntil.HasValue || days!.Contains(day.DayOfWeek))
            {
                var delta = day.DayNumber - startDate.DayNumber;
                items.Add(new MentorAvailabilityBatchSlotItem
                {
                    StartTime = request.StartTime.AddDays(delta),
                    EndTime = request.EndTime.AddDays(delta),
                });
            }
        return await CreateAvailabilityCoreAsync(
            actor.Id,
            items,
            request.IdempotencyKey,
            enforceBatchSubmissionCap: !request.RepeatUntil.HasValue,
            enforceStrictSessionDuration: false,
            cancellationToken);
    }

    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityBatchAsync(
        CreateMentorAvailabilityBatchRequest request, CancellationToken cancellationToken = default)
    {
        var actor = claimService.GetUserClaim();
        if (actor.Role != Role.Mentor)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Giáo viên được đăng ký lịch dạy.", HttpStatusCode.Forbidden);
        if (request?.Slots is null || request.Slots.Count == 0)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Vui lòng chọn ít nhất một khung giờ để đăng ký.", HttpStatusCode.BadRequest);

        return await CreateAvailabilityCoreAsync(
            actor.Id,
            request.Slots,
            request.IdempotencyKey,
            enforceBatchSubmissionCap: true,
            enforceStrictSessionDuration: true,
            cancellationToken);
    }

    private async Task<ApiResponse<List<MentorAvailabilityResponse>>> CreateAvailabilityCoreAsync(
        int mentorId,
        List<MentorAvailabilityBatchSlotItem> rawItems,
        string? idempotencyKey,
        bool enforceBatchSubmissionCap,
        bool enforceStrictSessionDuration,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var opts = slotOptions?.Value ?? new MentorSlotGenerationOptions();
        var maxPerSubmission = Math.Clamp(opts.MaxSlotsPerSubmission, 1, 50);
        var maxPendingSlots = Math.Clamp(opts.MaxPendingSlots, 1, 200);
        var cooldownSeconds = Math.Clamp(opts.SubmissionCooldownSeconds, 0, 120);

        // 1. Idempotency check (deduplicate double-clicks / retries within 10 minutes)
        var cleanIdemKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : $"{mentorId}:{idempotencyKey.Trim()}";
        if (cleanIdemKey is not null && IdempotencyRecords.TryGetValue(cleanIdemKey, out var cached) && cached.ExpiresUtc > nowUtc)
        {
            var existingRows = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
                .Where(x => x.MentorId == mentorId && cached.SlotIds.Contains(x.Id) && !x.IsDeleted)
                .OrderBy(x => x.StartTime)
                .ToListAsync(cancellationToken);
            if (existingRows.Count > 0)
                return ApiResponse<List<MentorAvailabilityResponse>>.Success(existingRows.Select(s => Map(s, string.Empty)).ToList());
        }

        if (rawItems.Count == 0 || rawItems.Count > 500)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Không có ngày phù hợp hoặc quá nhiều buổi.");

        if (enforceBatchSubmissionCap && rawItems.Count > maxPerSubmission)
        {
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail(
                $"Mỗi lần chỉ được gửi tối đa {maxPerSubmission} khung giờ đăng ký.",
                HttpStatusCode.BadRequest,
                "BATCH_LIMIT_EXCEEDED");
        }

        // Validate each candidate slot format & deduplicate identical intervals in the same request
        var seenIntervals = new HashSet<(DateTime Start, DateTime End)>();
        var batchStampUtc = new DateTime(nowUtc.Ticks - (nowUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        var rows = new List<MentorAvailability>(rawItems.Count);
        foreach (var item in rawItems.OrderBy(x => x.StartTime))
        {
            if (item.StartTime.Kind != DateTimeKind.Utc || item.EndTime.Kind != DateTimeKind.Utc
                || item.StartTime <= nowUtc || item.EndTime <= item.StartTime
                || item.EndTime - item.StartTime > TimeSpan.FromHours(8)
                || item.StartTime.AddHours(7).Date != item.EndTime.AddTicks(-1).AddHours(7).Date)
            {
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail(
                    "Chọn giờ rảnh trong tương lai, cùng ngày, tối đa 8 giờ và có múi giờ UTC.",
                    HttpStatusCode.BadRequest);
            }

            if (!seenIntervals.Add((item.StartTime, item.EndTime)))
            {
                return ApiResponse<List<MentorAvailabilityResponse>>.Fail(
                    "Danh sách đăng ký có khung giờ bị trùng nhau trong cùng lần gửi.",
                    HttpStatusCode.Conflict,
                    "DUPLICATE_SLOT_IN_BATCH");
            }

            rows.Add(new MentorAvailability
            {
                MentorId = mentorId,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                MaxLearners = 1,
                IsApproved = false,
                CreatedDate = batchStampUtc,
            });
        }

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(mentorId, mentorId, token);

                var policy = groupPolicies is null ? null : await groupPolicies.GetEffectiveAsync(rows[0].StartTime, token);
                var sessionMinutes = policy?.SessionMinutes ?? 90;
                var restMinutes = policy?.RestMinutes ?? 0;
                var dailyCap = policy?.DailySessionCap ?? opts.DailyBookingCap;
                var weeklyCap = policy?.WeeklySessionCap ?? 0;

                if (enforceStrictSessionDuration && policy is not null && sessionMinutes > 0)
                {
                    var invalidDuration = rows.FirstOrDefault(r => Math.Abs((r.EndTime - r.StartTime).TotalMinutes - sessionMinutes) > 0.1);
                    if (invalidDuration is not null)
                    {
                        throw new AvailabilityQuotaException(
                            $"Mỗi khung giờ đăng ký theo chính sách hiện hành phải đúng {sessionMinutes} phút.",
                            HttpStatusCode.BadRequest);
                    }
                }

                // Cooldown check backed by persisted CreatedDate (safe across instances & isolated test DbContexts)
                if (cooldownSeconds > 0 && enforceBatchSubmissionCap)
                {
                    var cutoffUtc = nowUtc.AddSeconds(-cooldownSeconds);
                    var recentSubmitUtc = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
                        .Where(x => x.MentorId == mentorId && !x.IsDeleted && x.CreatedDate != null && x.CreatedDate > cutoffUtc)
                        .OrderByDescending(x => x.CreatedDate)
                        .Select(x => (DateTime?)x.CreatedDate)
                        .FirstOrDefaultAsync(token);
                    if (recentSubmitUtc.HasValue && (nowUtc - recentSubmitUtc.Value).TotalSeconds < cooldownSeconds)
                    {
                        var waitSec = Math.Max(1, (int)Math.Ceiling(cooldownSeconds - (nowUtc - recentSubmitUtc.Value).TotalSeconds));
                        throw new AvailabilityQuotaException(
                            $"Bạn thao tác gửi đăng ký quá nhanh. Vui lòng chờ {waitSec} giây trước khi gửi tiếp.",
                            HttpStatusCode.TooManyRequests);
                    }
                }

                // Check intra-batch overlap & rest violation between the proposed rows themselves
                for (var i = 0; i < rows.Count; i++)
                {
                    for (var j = i + 1; j < rows.Count; j++)
                    {
                        var a = rows[i];
                        var b = rows[j];
                        if (a.StartTime < b.EndTime && b.StartTime < a.EndTime)
                            throw new AvailabilityOverlapException();

                        if (restMinutes > 0 && (b.StartTime - a.EndTime).TotalMinutes < restMinutes)
                        {
                            var sLocal = a.StartTime.AddHours(7);
                            var eLocal = a.EndTime.AddHours(7);
                            throw new AvailabilityRestConflictException(
                                $"Khung giờ quá sát lịch {sLocal:dd/MM HH:mm}–{eLocal:HH:mm}; cần nghỉ tối thiểu {restMinutes} phút giữa hai buổi.");
                        }
                    }
                }

                // Load mentor's active future slots + slots in affected weeks for atomic quota & conflict checks
                var firstVnDate = DateOnly.FromDateTime(rows[0].StartTime.AddHours(7));
                var lastVnDate = DateOnly.FromDateTime(rows[^1].EndTime.AddHours(7));
                var rangeStartMonday = MondayOfVnDate(firstVnDate);
                var rangeEndSunday = MondayOfVnDate(lastVnDate).AddDays(7);
                var queryMinUtc = rangeStartMonday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddHours(-7).AddMinutes(-restMinutes);
                var queryMaxUtc = rangeEndSunday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddHours(-7).AddMinutes(restMinutes);

                var activeDbSlots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
                    .Where(x => x.MentorId == mentorId && !x.IsDeleted && x.RejectedAt == null
                        && (x.StartTime >= nowUtc || (x.StartTime < queryMaxUtc && x.EndTime > queryMinUtc)))
                    .Select(x => new { x.Id, x.StartTime, x.EndTime, x.IsApproved })
                    .ToListAsync(token);

                // Total pending queue cap check
                if (enforceBatchSubmissionCap && maxPendingSlots > 0)
                {
                    var currentPendingCount = activeDbSlots.Count(x => !x.IsApproved && x.StartTime > nowUtc);
                    if (currentPendingCount + rows.Count > maxPendingSlots)
                    {
                        throw new AvailabilityQuotaException(
                            $"Bạn đang có {currentPendingCount} khung giờ chờ duyệt (tối đa {maxPendingSlots} khung chờ duyệt cùng lúc). Vui lòng chờ Admin duyệt hoặc rút bớt khung chờ duyệt trước khi gửi thêm.",
                            HttpStatusCode.Conflict);
                    }
                }

                // Overlap & Rest window check against existing DB slots
                foreach (var slot in rows)
                {
                    if (await unitOfWork.MentorAvailabilities.HasOverlappingSlotAsync(mentorId, slot.StartTime, slot.EndTime, token))
                        throw new AvailabilityOverlapException();

                    if (restMinutes > 0)
                    {
                        var tooClose = activeDbSlots.FirstOrDefault(x =>
                            !(x.StartTime < slot.EndTime && slot.StartTime < x.EndTime) &&
                            ((slot.StartTime >= x.EndTime && (slot.StartTime - x.EndTime).TotalMinutes < restMinutes) ||
                             (x.StartTime >= slot.EndTime && (x.StartTime - slot.EndTime).TotalMinutes < restMinutes)));
                        if (tooClose is not null)
                        {
                            var sLocal = tooClose.StartTime.AddHours(7);
                            var eLocal = tooClose.EndTime.AddHours(7);
                            throw new AvailabilityRestConflictException(
                                $"Khung giờ quá sát lịch {sLocal:dd/MM HH:mm}–{eLocal:HH:mm}; cần nghỉ tối thiểu {restMinutes} phút giữa hai buổi.");
                        }
                    }
                }

                // Daily Session Cap check (only when GroupPolicy is configured or enforceBatchSubmissionCap is true)
                if ((policy is not null || enforceBatchSubmissionCap) && dailyCap > 0)
                {
                    var existingByDay = activeDbSlots
                        .Where(x => x.StartTime < queryMaxUtc && x.EndTime > queryMinUtc)
                        .GroupBy(x => DateOnly.FromDateTime(x.StartTime.AddHours(7)))
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var dayGroup in rows.GroupBy(r => DateOnly.FromDateTime(r.StartTime.AddHours(7))))
                    {
                        var existingOnDay = existingByDay.GetValueOrDefault(dayGroup.Key, 0);
                        if (existingOnDay + dayGroup.Count() > dailyCap)
                        {
                            throw new AvailabilityQuotaException(
                                $"Bạn đã đạt giới hạn đăng ký lịch trong ngày {dayGroup.Key:dd/MM/yyyy} (tối đa {dailyCap} khung giờ/ngày, hiện đã có {existingOnDay} khung).",
                                HttpStatusCode.Conflict);
                        }
                    }
                }

                // Weekly Session Cap check (0 = unlimited)
                if (weeklyCap > 0)
                {
                    var existingByWeek = activeDbSlots
                        .Where(x => x.StartTime < queryMaxUtc && x.EndTime > queryMinUtc)
                        .GroupBy(x => MondayOfVnDate(DateOnly.FromDateTime(x.StartTime.AddHours(7))))
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var weekGroup in rows.GroupBy(r => MondayOfVnDate(DateOnly.FromDateTime(r.StartTime.AddHours(7)))))
                    {
                        var existingInWeek = existingByWeek.GetValueOrDefault(weekGroup.Key, 0);
                        if (existingInWeek + weekGroup.Count() > weeklyCap)
                        {
                            throw new AvailabilityQuotaException(
                                $"Bạn đã đạt giới hạn đăng ký lịch trong tuần này (tối đa {weeklyCap} khung giờ/tuần, hiện đã có {existingInWeek} khung).",
                                HttpStatusCode.Conflict);
                        }
                    }
                }

                await unitOfWork.MentorAvailabilities.AddRangeAsync(rows, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            if (cleanIdemKey is not null && rows.Count > 0)
            {
                IdempotencyRecords[cleanIdemKey] = (nowUtc.AddMinutes(10), rows.Select(r => r.Id).ToList());
            }

            return ApiResponse<List<MentorAvailabilityResponse>>.Success(
                rows.Select(s => Map(s, $"{s.Mentor?.FirstName} {s.Mentor?.LastName}".Trim())).ToList());
        }
        catch (AvailabilityOverlapException)
        {
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Có ngày/giờ trùng lịch; chưa lưu đăng ký nào.", HttpStatusCode.Conflict);
        }
        catch (AvailabilityRestConflictException ex)
        {
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail(ex.Message, HttpStatusCode.Conflict);
        }
        catch (AvailabilityQuotaException ex)
        {
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail(ex.Message, ex.Status);
        }
    }

    private static DateOnly MondayOfVnDate(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var offset = dow == 0 ? -6 : 1 - dow;
        return date.AddDays(offset);
    }

    private sealed class AvailabilityOverlapException : Exception;
    private sealed class AvailabilityRestConflictException(string message) : Exception(message);
    private sealed class AvailabilityQuotaException(string message, HttpStatusCode status) : Exception(message)
    {
        public HttpStatusCode Status { get; } = status;
    }

    public async Task<ApiResponse<bool>> DeleteAvailabilityAsync(int id, CancellationToken cancellationToken = default)
    {
        var claims = claimService.GetUserClaim();
        var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
        if (slot == null || slot.IsDeleted)
            return ApiResponse<bool>.Fail("Không tìm thấy khung giờ.", HttpStatusCode.NotFound);
        if (slot.MentorId != claims.Id && claims.Role != Role.Admin)
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa khung giờ này.", HttpStatusCode.Forbidden);
        if (slot.IsApproved || slot.BookedCount > 0 || slot.RejectedAt != null || slot.StartTime <= DateTime.UtcNow)
            return ApiResponse<bool>.Fail("Lịch đã duyệt, đã xử lý hoặc đã bắt đầu không thể xóa trực tiếp.", HttpStatusCode.Conflict);

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
        if (!approve && string.IsNullOrWhiteSpace(rejectionReason))
            return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối yêu cầu hủy.");
        if (rejectionReason?.Length > 1000)
            return ApiResponse<bool>.Fail("Lý do từ chối tối đa 1000 ký tự.");

        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<bool>.Fail("Chỉ Admin mới được duyệt yêu cầu hủy.", HttpStatusCode.Forbidden);

        ApiResponse<bool>? error = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(token);
            if (slots.Count != distinctIds.Count)
            { error = ApiResponse<bool>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound); return; }
            if (slots.Any(x => x.CancelRequestedAt is null))
            { error = ApiResponse<bool>.Fail("Có khung giờ chưa có yêu cầu hủy.", HttpStatusCode.Conflict); return; }

            var mentorIds = slots.Select(s => s.MentorId).Distinct().OrderBy(id => id).ToList();
            foreach (var mid in mentorIds) await unitOfWork.StudyCourses.LockScheduleAsync(mid, mid, token);

            var now = DateTime.UtcNow;
            if (approve && (slots.Any(x => x.BookedCount > 0)
                || await unitOfWork.StudyCohorts.AnyAsync(c => c.Sessions.Any(s => distinctIds.Contains(s.AvailabilityId)), token)
                || await unitOfWork.StudyCourses.AnyAsync(c => c.PaymentStatus == "Paid" && c.Status != "Cancelled"
                    && c.Sessions.Any(s => s.AvailabilityId.HasValue && distinctIds.Contains(s.AvailabilityId.Value)), token)))
            { error = ApiResponse<bool>.Fail("Có khung giờ đã được đặt hoặc gán vào lớp học nên không thể duyệt hủy.", HttpStatusCode.Conflict); return; }

            foreach (var slot in slots)
            {
                if (approve)
                    slot.IsDeleted = true;
                else
                {
                    slot.CancelRequestedAt = null;
                    slot.CancelRejectedAt = now;
                    slot.CancelRejectionReason = rejectionReason!.Trim();
                }
                slot.ModifiedDate = now;
                unitOfWork.MentorAvailabilities.Update(slot);
            }

            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = claims.Id,
                EntityType = "MentorAvailabilityCancelBatch",
                EntityId = slots[0].Id,
                Action = approve ? "Approved" : "Rejected",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { SlotIds = distinctIds, Approve = approve }),
                Reason = rejectionReason?.Trim(),
                CreatedAtUtc = now,
            }, token);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return error ?? ApiResponse<bool>.Success(true);
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

        ApiResponse<bool>? error = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var slots = await unitOfWork.MentorAvailabilities.Query()
                .Include(x => x.GroupPolicy)
                .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
                .OrderBy(x => x.MentorId).ThenBy(x => x.StartTime)
                .ToListAsync(token);
            var now = DateTime.UtcNow;
            if (slots.Count != distinctIds.Count)
            { error = ApiResponse<bool>.Fail("Có khung giờ không tồn tại.", HttpStatusCode.NotFound); return; }

            var mentorIds = slots.Select(s => s.MentorId).Distinct().OrderBy(id => id).ToList();
            foreach (var mid in mentorIds) await unitOfWork.StudyCourses.LockScheduleAsync(mid, mid, token);

            if (slots.Any(x => x.IsApproved || x.RejectedAt is not null))
            { error = ApiResponse<bool>.Fail("Có lịch đã được xử lý.", HttpStatusCode.Conflict); return; }
            if (slots.Any(x => x.StartTime <= now))
            { error = ApiResponse<bool>.Fail("Có lịch đã quá hạn, không thể duyệt.", HttpStatusCode.Conflict); return; }
            if (slots.Any(x => x.BookedCount > 0))
            { error = ApiResponse<bool>.Fail("Có lịch đã có học viên đặt, không thể thay đổi trạng thái.", HttpStatusCode.Conflict); return; }

            if (approve)
            {
                var mentors = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .Where(u => mentorIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, token);

                foreach (var slot in slots)
                {
                    if (!mentors.TryGetValue(slot.MentorId, out var mentor) || mentor.IsDeleted || mentor.Role != Role.Mentor
                        || mentor.MentorProfile?.VerificationStatus != MentorVerificationStatus.Approved)
                    {
                        error = ApiResponse<bool>.Fail("Chỉ được duyệt lịch cho Giáo viên đang hoạt động và đã được duyệt hồ sơ.", HttpStatusCode.Conflict);
                        return;
                    }

                    var policyErr = await ValidateSlotPolicyCapsAsync(slot, slots, token);
                    if (policyErr is not null)
                    {
                        error = ApiResponse<bool>.Fail(policyErr, HttpStatusCode.Conflict);
                        return;
                    }
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

            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = claims.Id,
                EntityType = "MentorAvailabilityBatch",
                EntityId = slots[0].Id,
                Action = approve ? "Approved" : "Rejected",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { SlotIds = distinctIds, Approve = approve }),
                Reason = rejectionReason?.Trim(),
                CreatedAtUtc = now,
            }, token);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return error ?? ApiResponse<bool>.Success(true);
    }

    private static List<int> NormalizeIds(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    private sealed record CohortSlotContext(
        int CohortId,
        string CohortName,
        string CohortStatus,
        int SessionNumber,
        int EnrolledCount,
        int Capacity,
        bool SessionCompleted,
        int AttendanceConfirmedCount);

    private async Task<Dictionary<int, CohortSlotContext>> LoadCohortSlotContextsAsync(
        IReadOnlyCollection<int> slotIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, CohortSlotContext>();
        if (slotIds.Count == 0 || unitOfWork.StudyCohorts is null)
            return result;

        var idSet = slotIds.ToHashSet();
        var cohorts = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Where(c => c.Status != "Cancelled" && c.Sessions.Any(s => idSet.Contains(s.AvailabilityId)))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.Capacity,
                c.CapacitySnapshot,
                Sessions = c.Sessions
                    .Where(s => idSet.Contains(s.AvailabilityId))
                    .Select(s => new { s.AvailabilityId, s.Number })
                    .ToList(),
                Enrollments = c.Enrollments
                    .Select(e => new { e.Id, e.PaymentStatus, e.Status, e.PaymentExpiresAt })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var attendanceByAvailability = new Dictionary<int, (int ConfirmedCount, bool HasScheduled)>();
        if (cohorts.Count > 0 && unitOfWork.StudyCourses is not null)
        {
            var courseSessions = await unitOfWork.StudyCourses.Query().AsNoTracking()
                .SelectMany(c => c.Sessions
                    .Where(cs => cs.AvailabilityId.HasValue && idSet.Contains(cs.AvailabilityId.Value))
                    .Select(cs => new
                    {
                        AvailabilityId = cs.AvailabilityId!.Value,
                        cs.Status,
                        CourseStatus = c.Status,
                    }))
                .ToListAsync(cancellationToken);

            foreach (var grp in courseSessions.GroupBy(x => x.AvailabilityId))
            {
                var relevant = grp
                    .Where(x => x.CourseStatus != "Cancelled" && x.CourseStatus != "Transferred")
                    .ToList();
                var confirmed = relevant.Count(x =>
                    x.Status is "Present" or "Absent" or "ExcusedAbsent");
                var hasScheduled = relevant.Any(x => x.Status == "Scheduled");
                attendanceByAvailability[grp.Key] = (confirmed, hasScheduled);
            }
        }

        foreach (var cohort in cohorts)
        {
            var enrolledCount = cohort.Enrollments.Count(e =>
                (e.PaymentStatus == "Paid" && e.Status != "PaymentReview" && e.Status != "Cancelled" && e.Status != "Transferred")
                || (e.PaymentStatus == "Pending" && e.PaymentExpiresAt > nowUtc));
            var capacity = Math.Max(0, cohort.CapacitySnapshot ?? cohort.Capacity);

            foreach (var sess in cohort.Sessions)
            {
                if (result.ContainsKey(sess.AvailabilityId)) continue;
                attendanceByAvailability.TryGetValue(sess.AvailabilityId, out var att);
                var sessionCompleted = string.Equals(cohort.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    || (att.ConfirmedCount > 0 && !att.HasScheduled);
                result[sess.AvailabilityId] = new CohortSlotContext(
                    CohortId: cohort.Id,
                    CohortName: cohort.Name,
                    CohortStatus: cohort.Status,
                    SessionNumber: sess.Number,
                    EnrolledCount: enrolledCount,
                    Capacity: capacity,
                    SessionCompleted: sessionCompleted,
                    AttendanceConfirmedCount: att.ConfirmedCount);
            }
        }

        return result;
    }

    private static MentorAvailabilityResponse Map(
        MentorAvailability slot,
        string mentorName,
        CohortSlotContext? cohort = null,
        DateTime? nowUtcOverride = null)
    {
        _ = nowUtcOverride;
        var inCohort = cohort is not null;
        var pendingCancel = slot.CancelRequestedAt != null && slot.CancelRejectedAt == null && !slot.IsDeleted;
        var pendingChange = slot.ChangeRequestedAt != null && slot.ChangeResolution == null && !slot.IsDeleted;
        var isCompletedSession = inCohort && cohort!.SessionCompleted;
        var isBooked = inCohort || slot.IsFull || slot.BookedCount > 0;
        var status = slot.IsDeleted
            ? "Cancelled"
            : slot.RejectedAt != null
                ? "Rejected"
                : (!slot.IsApproved || pendingCancel || pendingChange)
                    ? "Pending"
                    : isCompletedSession
                        ? "Completed"
                        : isBooked
                            ? "Booked"
                            : "Approved";

        return new MentorAvailabilityResponse
        {
            Id = slot.Id,
            MentorId = slot.MentorId,
            MentorName = mentorName,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsApproved = slot.IsApproved,
            RejectedAt = slot.RejectedAt,
            RejectionReason = slot.RejectionReason,
            IsBooked = slot.IsFull || isBooked,
            BookedCount = inCohort ? cohort!.EnrolledCount : slot.BookedCount,
            MaxLearners = inCohort ? cohort!.Capacity : slot.MaxLearners,
            AdminName = slot.AdminName,
            AdvanceDaysAtCreation = slot.CreatedDate is DateTime createdAt ? (int)(slot.StartTime - createdAt).TotalDays : 0,
            MeetsAdvanceRule = slot.CreatedDate is DateTime created && slot.StartTime >= created.AddMonths(MinAdvanceMonths),
            CancelRequestedAt = slot.CancelRequestedAt,
            CancelReason = slot.CancelReason,
            CancelRejectedAt = slot.CancelRejectedAt,
            CancelRejectionReason = slot.CancelRejectionReason,
            MentorWeeklyAvailabilityId = slot.MentorWeeklyAvailabilityId,
            PublishedAt = slot.PublishedAt,
            ChangeRequestReason = slot.ChangeRequestReason,
            ChangeRequestedAt = slot.ChangeRequestedAt,
            ChangeResolution = slot.ChangeResolution,
            IsInCohort = inCohort,
            CohortId = cohort?.CohortId,
            CohortName = cohort?.CohortName,
            CohortStatus = cohort?.CohortStatus,
            SessionNumber = cohort?.SessionNumber,
            SessionCompleted = isCompletedSession,
            AttendanceConfirmedCount = cohort?.AttendanceConfirmedCount ?? 0,
            IsDeleted = slot.IsDeleted,
            Status = status,
            EventType = inCohort ? "CohortSession" : "Availability",
            CreatedAt = slot.CreatedDate,
            BatchGroupKey = slot.CreatedDate is DateTime batchDt
                ? $"M{slot.MentorId}-{batchDt:yyyyMMddHHmmss}"
                : $"M{slot.MentorId}-SINGLE"
        };
    }
}
