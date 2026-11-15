using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorDiscovery;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorCompensation;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

public sealed class MentorDiscoveryService(
    IUnitOfWork unitOfWork,
    ILogger<MentorDiscoveryService> logger) : IMentorDiscoveryService
{
    private static readonly TimeZoneInfo DefaultTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId);

    public async Task<ApiResponse<PagedResult<PublicMentorCardResponse>>> SearchMentorsAsync(
        MentorDiscoveryFilterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            int? requiredTeachingLevelId = null;
            if (!string.IsNullOrWhiteSpace(request.TeachingJlptLevel))
            {
                if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.TeachingJlptLevel, out var normalizedTeachingLevel))
                {
                    return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                        "Chỉ hỗ trợ lọc cố vấn dạy trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).",
                        HttpStatusCode.BadRequest);
                }

                requiredTeachingLevelId = await ResolveCertificateLevelIdAsync(normalizedTeachingLevel, cancellationToken);
            }

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 50);

            var (mentors, totalCount) = await unitOfWork.MentorProfiles.SearchApprovedMentorsAsync(
                request.SearchTerm,
                request.Expertise,
                request.JlptLevel,
                page,
                pageSize,
                request.SortBy,
                cancellationToken);

            var mentorList = mentors.ToList();
            var mentorIds = mentorList.Select(m => m.Id).ToList();
            var teachingLevelsByMentor = await LoadActiveTeachingLevelsAsync(mentorIds, cancellationToken);

            if (requiredTeachingLevelId.HasValue && unitOfWork.MentorTeachingQualifications != null)
            {
                var qualifiedMentorIds = await unitOfWork.MentorTeachingQualifications.Query()
                    .Where(q => q.IsActive && q.RevokedAtUtc == null && q.CertificateLevelId == requiredTeachingLevelId.Value)
                    .Select(q => q.MentorUserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var qualifiedSet = qualifiedMentorIds.ToHashSet();
                mentorList = mentorList.Where(m => qualifiedSet.Contains(m.Id)).ToList();
                totalCount = mentorList.Count;
            }

            var items = mentorList.Select(u => new PublicMentorCardResponse
            {
                MentorId = u.Id,
                Name = $"{u.FirstName} {u.LastName}".Trim(),
                AvatarUrl = u.AvatarUrl,
                Bio = u.MentorProfile?.Bio,
                Expertise = u.MentorProfile?.Expertise,
                JlptLevel = u.MentorProfile?.JlptLevel,
                TeachingJlptLevels = teachingLevelsByMentor.TryGetValue(u.Id, out var levels) ? levels : [],
                YearsOfExperience = u.MentorProfile?.YearsOfExperience,
                Workplace = u.MentorProfile?.Workplace,
                IntroductionVideoUrl = u.MentorProfile?.IntroductionVideoUrl,
                IsBookable = true
            }).ToList();

            var pagedResult = new PagedResult<PublicMentorCardResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return ApiResponse<PagedResult<PublicMentorCardResponse>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra khi tìm kiếm danh sách chuyên gia công khai.");
            return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                "Không thể tải danh sách chuyên gia.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PublicMentorProfileResponse>> GetMentorProfileAsync(
        int mentorId, CancellationToken cancellationToken = default)
    {
        if (mentorId <= 0)
            return ApiResponse<PublicMentorProfileResponse>.Fail("Mã chuyên gia không hợp lệ.");

        try
        {
            var mentor = await unitOfWork.UserAccounts.Query()
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);

            if (mentor is null || mentor.MentorProfile is null || mentor.MentorProfile.VerificationStatus != MentorVerificationStatus.Approved)
                return ApiResponse<PublicMentorProfileResponse>.Fail(
                    "Không tìm thấy thông tin chuyên gia được phê duyệt công khai.", HttpStatusCode.NotFound);

            var teachingLevelsByMentor = await LoadActiveTeachingLevelsAsync([mentor.Id], cancellationToken);
            var profile = mentor.MentorProfile;
            var response = new PublicMentorProfileResponse
            {
                MentorId = mentor.Id,
                Name = $"{mentor.FirstName} {mentor.LastName}".Trim(),
                AvatarUrl = mentor.AvatarUrl,
                Bio = profile.Bio,
                Expertise = profile.Expertise,
                JlptLevel = profile.JlptLevel,
                TeachingJlptLevels = teachingLevelsByMentor.TryGetValue(mentor.Id, out var levels) ? levels : [],
                YearsOfExperience = profile.YearsOfExperience,
                Workplace = profile.Workplace,
                IntroductionVideoUrl = profile.IntroductionVideoUrl,
                IsBookable = true
            };

            return ApiResponse<PublicMentorProfileResponse>.Success(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra khi lấy thông tin hồ sơ chuyên gia ID {MentorId}.", mentorId);
            return ApiResponse<PublicMentorProfileResponse>.Fail(
                "Không thể tải thông tin chuyên gia.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PublicMentorSlotsGroupedResponse>> GetPublicSlotsAsync(
        int mentorId, MentorSlotBrowsingRequest request, CancellationToken cancellationToken = default)
    {
        if (mentorId <= 0)
            return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail("Mã chuyên gia không hợp lệ.");

        try
        {
            if (!string.IsNullOrWhiteSpace(request.JlptLevel))
            {
                if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out var normalizedLevel))
                {
                    return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail(
                        "Chỉ hỗ trợ đặt lịch tư vấn cho trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).",
                        HttpStatusCode.BadRequest);
                }

                var levelId = await ResolveCertificateLevelIdAsync(normalizedLevel, cancellationToken);
                var isQualified = await IsMentorQualifiedForLevelAsync(mentorId, levelId, cancellationToken);
                if (!isQualified)
                {
                    return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail(
                        $"Chuyên gia chưa được cấp quyền giảng dạy trình độ JLPT {normalizedLevel}.",
                        HttpStatusCode.Conflict);
                }
            }

            var mentor = await unitOfWork.UserAccounts.Query()
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);

            if (mentor is null || mentor.MentorProfile is null || mentor.MentorProfile.VerificationStatus != MentorVerificationStatus.Approved)
                return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail(
                    "Chuyên gia không tồn tại hoặc chưa được phê duyệt.", HttpStatusCode.NotFound);

            var tz = ResolveTimeZone(request.TimeZoneId);
            var (fromUtc, toUtc, error) = ResolveTimeRange(request, tz);
            if (error is not null)
                return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail(error);

            var slots = await unitOfWork.MentorAvailabilities.GetPublicBookableSlotsAsync(
                mentorId, fromUtc, toUtc, cancellationToken);

            var slotResponses = slots.Select(s => MapToPublicSlot(s, tz)).ToList();

            var groupedDaily = slotResponses
                .GroupBy(s => s.LocalDate)
                .OrderBy(g => g.Key)
                .Select(g => new PublicMentorDailySlotsResponse
                {
                    Date = g.Key,
                    Slots = g.OrderBy(s => s.StartTimeUtc).ToList()
                })
                .ToList();

            var fromLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz));
            var toLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(toUtc, tz));

            var result = new PublicMentorSlotsGroupedResponse
            {
                MentorId = mentor.Id,
                MentorName = $"{mentor.FirstName} {mentor.LastName}".Trim(),
                TimeZoneId = tz.Id,
                FromDate = fromLocal,
                ToDate = toLocal,
                TotalAvailableSlots = slotResponses.Count,
                DailySlots = groupedDaily
            };

            return ApiResponse<PublicMentorSlotsGroupedResponse>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra khi duyệt lịch của chuyên gia ID {MentorId}.", mentorId);
            return ApiResponse<PublicMentorSlotsGroupedResponse>.Fail(
                "Không thể tải lịch khả dụng của chuyên gia.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<DateOnly>>> GetAvailableDatesAsync(
        int mentorId, MentorSlotBrowsingRequest request, CancellationToken cancellationToken = default)
    {
        if (mentorId <= 0)
            return ApiResponse<List<DateOnly>>.Fail("Mã chuyên gia không hợp lệ.");

        try
        {
            if (!string.IsNullOrWhiteSpace(request.JlptLevel))
            {
                if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(request.JlptLevel, out var normalizedLevel))
                {
                    return ApiResponse<List<DateOnly>>.Fail(
                        "Chỉ hỗ trợ đặt lịch tư vấn cho trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).",
                        HttpStatusCode.BadRequest);
                }

                var levelId = await ResolveCertificateLevelIdAsync(normalizedLevel, cancellationToken);
                var isQualified = await IsMentorQualifiedForLevelAsync(mentorId, levelId, cancellationToken);
                if (!isQualified)
                {
                    return ApiResponse<List<DateOnly>>.Fail(
                        $"Chuyên gia chưa được cấp quyền giảng dạy trình độ JLPT {normalizedLevel}.",
                        HttpStatusCode.Conflict);
                }
            }

            var mentor = await unitOfWork.UserAccounts.Query()
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);

            if (mentor is null || mentor.MentorProfile is null || mentor.MentorProfile.VerificationStatus != MentorVerificationStatus.Approved)
                return ApiResponse<List<DateOnly>>.Fail(
                    "Chuyên gia không tồn tại hoặc chưa được phê duyệt.", HttpStatusCode.NotFound);

            var tz = ResolveTimeZone(request.TimeZoneId);
            var (fromUtc, toUtc, error) = ResolveTimeRange(request, tz);
            if (error is not null)
                return ApiResponse<List<DateOnly>>.Fail(error);

            var slots = await unitOfWork.MentorAvailabilities.GetPublicBookableSlotsAsync(
                mentorId, fromUtc, toUtc, cancellationToken);

            var dates = slots
                .Select(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(s.StartTime, tz)))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return ApiResponse<List<DateOnly>>.Success(dates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra khi lấy danh sách ngày có lịch của chuyên gia ID {MentorId}.", mentorId);
            return ApiResponse<List<DateOnly>>.Fail(
                "Không thể tải danh sách ngày làm việc.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PublicMentorSlotResponse>> GetPublicSlotByIdAsync(
        int slotId, string? timeZoneId = null, CancellationToken cancellationToken = default)
    {
        if (slotId <= 0)
            return ApiResponse<PublicMentorSlotResponse>.Fail("Mã khung giờ không hợp lệ.");

        try
        {
            var slot = await unitOfWork.MentorAvailabilities.GetPublicBookableSlotByIdAsync(
                slotId, cancellationToken);

            if (slot is null)
                return ApiResponse<PublicMentorSlotResponse>.Fail(
                    "Khung giờ không tồn tại, đã có người đặt hoặc không khả dụng.", HttpStatusCode.NotFound);

            var tz = ResolveTimeZone(timeZoneId);
            return ApiResponse<PublicMentorSlotResponse>.Success(MapToPublicSlot(slot, tz));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra khi lấy thông tin chi tiết khung giờ ID {SlotId}.", slotId);
            return ApiResponse<PublicMentorSlotResponse>.Fail(
                "Không thể tải thông tin khung giờ.", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<int> ResolveCertificateLevelIdAsync(string normalizedLevel, CancellationToken ct)
    {
        if (unitOfWork.CertificateLevels != null)
        {
            var certLevel = await unitOfWork.CertificateLevels.Query()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code.ToUpper() == normalizedLevel, ct);
            if (certLevel != null)
                return certLevel.Id;
        }
        return MentorCompensationPolicy.ResolveDefaultCertificateLevelId(normalizedLevel);
    }

    private async Task<bool> IsMentorQualifiedForLevelAsync(int mentorId, int certificateLevelId, CancellationToken ct)
    {
        if (unitOfWork.MentorTeachingQualifications == null)
            return false;

        return await unitOfWork.MentorTeachingQualifications.Query()
            .AnyAsync(q => q.MentorUserId == mentorId
                && q.CertificateLevelId == certificateLevelId
                && q.IsActive
                && q.RevokedAtUtc == null, ct);
    }

    private async Task<Dictionary<int, List<string>>> LoadActiveTeachingLevelsAsync(
        IReadOnlyCollection<int> mentorIds, CancellationToken ct)
    {
        var result = new Dictionary<int, List<string>>();
        if (mentorIds.Count == 0 || unitOfWork.MentorTeachingQualifications == null)
            return result;

        var rows = await unitOfWork.MentorTeachingQualifications.Query()
            .Include(q => q.CertificateLevel)
            .Where(q => q.IsActive && q.RevokedAtUtc == null && mentorIds.Contains(q.MentorUserId))
            .ToListAsync(ct);

        foreach (var group in rows.GroupBy(x => x.MentorUserId))
        {
            var levels = group
                .Select(x => x.CertificateLevel?.Code ?? MentorPlanService.ResolveLevelCode(x.CertificateLevelId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            result[group.Key] = levels;
        }

        return result;
    }

    private static (DateTime fromUtc, DateTime toUtc, string? error) ResolveTimeRange(
        MentorSlotBrowsingRequest request, TimeZoneInfo tz)
    {
        var nowUtc = DateTime.UtcNow;
        var minLeadUtc = nowUtc.AddHours(MentorSlotPolicy.MinimumBookingLeadHours);
        var maxHorizonUtc = nowUtc.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays);

        DateTime fromUtc;
        DateTime toUtc;

        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            if (request.EndDate.Value < request.StartDate.Value)
                return (default, default, "Ngày kết thúc không được trước ngày bắt đầu.");

            var startLocal = DateTime.SpecifyKind(request.StartDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind(request.EndDate.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified);

            fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        }
        else if (request.FromUtc.HasValue && request.ToUtc.HasValue)
        {
            if (request.ToUtc.Value < request.FromUtc.Value)
                return (default, default, "Thời gian kết thúc không được trước thời gian bắt đầu.");

            fromUtc = request.FromUtc.Value;
            toUtc = request.ToUtc.Value;
        }
        else
        {
            var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
            var endLocal = todayLocal.AddDays(14);
            var startLocal = DateTime.SpecifyKind(todayLocal.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var endLocalDt = DateTime.SpecifyKind(endLocal.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified);

            fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalDt, tz);
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
            return (default, default, $"Khoảng thời gian duyệt lịch tối đa là {MentorSlotPolicy.MaximumScheduleHorizonDays} ngày.");

        if (fromUtc < minLeadUtc) fromUtc = minLeadUtc;
        if (toUtc > maxHorizonUtc) toUtc = maxHorizonUtc;

        if (toUtc <= fromUtc)
            return (default, default, "Không còn khung giờ nào khả dụng trong khoảng thời gian đã chọn (yêu cầu tối thiểu 2 giờ trước buổi học).");

        return (fromUtc, toUtc, null);
    }

    private static PublicMentorSlotResponse MapToPublicSlot(MentorAvailability slot, TimeZoneInfo tz)
    {
        var localStartDt = TimeZoneInfo.ConvertTimeFromUtc(slot.StartTime, tz);
        var localEndDt = TimeZoneInfo.ConvertTimeFromUtc(slot.EndTime, tz);
        var localDate = DateOnly.FromDateTime(localStartDt);
        var localStart = TimeOnly.FromDateTime(localStartDt);
        var localEnd = TimeOnly.FromDateTime(localEndDt);
        var durationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;

        return new PublicMentorSlotResponse
        {
            SlotId = slot.Id,
            MentorId = slot.MentorId,
            StartTimeUtc = slot.StartTime,
            EndTimeUtc = slot.EndTime,
            LocalDate = localDate,
            LocalStart = localStart,
            LocalEnd = localEnd,
            DurationMinutes = durationMinutes,
            DaypartCode = ResolveDaypart(localStart),
            Status = "Available",
            IsAvailable = true
        };
    }

    private static string ResolveDaypart(TimeOnly localStart)
    {
        if (localStart < new TimeOnly(11, 0)) return "Morning";
        if (localStart < new TimeOnly(13, 0)) return "Noon";
        if (localStart < new TimeOnly(18, 0)) return "Afternoon";
        return "Evening";
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return DefaultTimeZone;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return DefaultTimeZone;
        }
    }
}
