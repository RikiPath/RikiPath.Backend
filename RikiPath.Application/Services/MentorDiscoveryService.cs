using System.Globalization;
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
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

public sealed class MentorDiscoveryService(
    IUnitOfWork unitOfWork,
    ILogger<MentorDiscoveryService> logger) : IMentorDiscoveryService
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    private sealed record RecruitingCohortMatchSummary(
        int TotalRecruitingCount,
        int OpenRecruitingCount,
        int MatchingRecruitingCount,
        int MatchingOpenCount,
        bool HasLevelRecruitingCohort);

    public async Task<ApiResponse<PagedResult<PublicMentorCardResponse>>> SearchMentorsAsync(
        MentorDiscoveryFilterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string? normalizedTeachingLevel = null;
            if (!string.IsNullOrWhiteSpace(request.TeachingJlptLevel))
            {
                if (!TryNormalizeSupportedLevel(request.TeachingJlptLevel, out var parsedLevel))
                {
                    return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                        "Chỉ hỗ trợ lọc Giáo viên dạy trình độ JLPT N5, N4, N3, N2 hoặc N1.",
                        HttpStatusCode.BadRequest);
                }
                normalizedTeachingLevel = parsedLevel;
            }

            if (!TryParseWeekdaysFilter(request.Weekdays, out var requestedWeekdays))
            {
                return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                    "Ngày có thể học phải là các thứ hợp lệ từ 0 (Chủ nhật) đến 6 (Thứ 7).",
                    HttpStatusCode.BadRequest);
            }

            if (!TryParseClockMinutes(request.FromTime, out var fromMinute)
                || !TryParseClockMinutes(request.ToTime, out var toMinute))
            {
                return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                    "Khung giờ học mong muốn không đúng định dạng HH:mm.",
                    HttpStatusCode.BadRequest);
            }

            if (fromMinute.HasValue && toMinute.HasValue && fromMinute.Value >= toMinute.Value)
            {
                return ApiResponse<PagedResult<PublicMentorCardResponse>>.Fail(
                    "Giờ bắt đầu khung giờ học phải sớm hơn giờ kết thúc.",
                    HttpStatusCode.BadRequest);
            }

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 50);

            var allApprovedMentors = await unitOfWork.MentorProfiles.GetApprovedMentorsForDiscoveryAsync(
                request.SearchTerm,
                request.Expertise,
                request.JlptLevel,
                cancellationToken);

            var mentorList = allApprovedMentors.ToList();
            var mentorIds = mentorList.Select(m => m.Id).ToList();

            var teachingLevelsByMentor = await LoadActiveTeachingLevelsAsync(mentorIds, cancellationToken);
            var ratingsByMentor = await LoadPublicReviewSummariesAsync(mentorIds, cancellationToken);
            var recruitingByMentor = await LoadRecruitingCohortsByMentorAsync(
                mentorIds,
                normalizedTeachingLevel,
                requestedWeekdays,
                fromMinute,
                toMinute,
                cancellationToken);

            var hasScheduleCriteria = requestedWeekdays.Count > 0 || fromMinute.HasValue || toMinute.HasValue;

            var enriched = new List<(PublicMentorCardResponse Card, int MatchRank, double AvgRatingVal, int ExpVal)>(mentorList.Count);
            foreach (var u in mentorList)
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                var teachingLevels = teachingLevelsByMentor.TryGetValue(u.Id, out var lvls) ? lvls : [];
                ratingsByMentor.TryGetValue(u.Id, out var ratingInfo);
                var recruiting = recruitingByMentor.TryGetValue(u.Id, out var recSummary)
                    ? recSummary
                    : new RecruitingCohortMatchSummary(0, 0, 0, 0, false);

                var teachesRequestedLevel = string.IsNullOrEmpty(normalizedTeachingLevel)
                    || teachingLevels.Contains(normalizedTeachingLevel, StringComparer.OrdinalIgnoreCase)
                    || recruiting.HasLevelRecruitingCohort;

                if (!string.IsNullOrEmpty(normalizedTeachingLevel) && !teachesRequestedLevel)
                    continue;

                if (request.OnlyRecruiting && recruiting.TotalRecruitingCount <= 0)
                    continue;

                // Khi có bộ lọc lịch học (ngày / khung giờ), chỉ giữ Giáo viên có ít nhất 1 lớp đang tuyển sinh
                // mà TOÀN BỘ lịch học bắt buộc của lớp khớp với lịch học viên đã chọn.
                if (hasScheduleCriteria && recruiting.MatchingRecruitingCount <= 0)
                    continue;

                var card = new PublicMentorCardResponse
                {
                    MentorId = u.Id,
                    Name = name,
                    AvatarUrl = u.AvatarUrl,
                    Bio = u.MentorProfile?.Bio,
                    Expertise = u.MentorProfile?.Expertise,
                    JlptLevel = u.MentorProfile?.JlptLevel,
                    TeachingJlptLevels = teachingLevels,
                    YearsOfExperience = u.MentorProfile?.YearsOfExperience,
                    Workplace = u.MentorProfile?.Workplace,
                    IntroductionVideoUrl = u.MentorProfile?.IntroductionVideoUrl,
                    IsBookable = true,
                    AverageRating = ratingInfo.ReviewCount > 0 ? ratingInfo.AverageRating : null,
                    ReviewCount = ratingInfo.ReviewCount,
                    RecruitingCohortCount = recruiting.TotalRecruitingCount,
                    OpenRecruitingCohortCount = recruiting.OpenRecruitingCount,
                    MatchingRecruitingCohortCount = recruiting.MatchingRecruitingCount,
                };

                // Nhóm mức độ phù hợp từ dữ liệu thực tế (không tạo điểm giả, không mặc định rating cao luôn phù hợp):
                // 3 = có lớp mở còn chỗ khớp toàn bộ điều kiện lọc, 2 = có lớp đang tuyển sinh khớp điều kiện (kể cả vừa đủ sĩ số),
                // 1 = có lớp đang tuyển sinh còn chỗ phù hợp tổng thể, 0 = chưa mở lớp tuyển sinh.
                var matchRank = recruiting.MatchingOpenCount > 0
                    ? 3
                    : recruiting.MatchingRecruitingCount > 0
                        ? 2
                        : recruiting.OpenRecruitingCount > 0
                            ? 1
                            : 0;

                enriched.Add((card, matchRank, card.AverageRating ?? -1.0, card.YearsOfExperience ?? -1));
            }

            var sortKey = request.SortBy?.Trim().ToLowerInvariant();
            IOrderedEnumerable<(PublicMentorCardResponse Card, int MatchRank, double AvgRatingVal, int ExpVal)> ordered = sortKey switch
            {
                "rating_desc" => enriched
                    .OrderByDescending(x => x.Card.ReviewCount > 0 ? 1 : 0)
                    .ThenByDescending(x => x.AvgRatingVal)
                    .ThenByDescending(x => x.Card.ReviewCount)
                    .ThenByDescending(x => x.MatchRank)
                    .ThenByDescending(x => x.ExpVal)
                    .ThenBy(x => x.Card.Name, StringComparer.CurrentCultureIgnoreCase),
                "experience_desc" => enriched
                    .OrderByDescending(x => x.ExpVal)
                    .ThenByDescending(x => x.MatchRank)
                    .ThenByDescending(x => x.AvgRatingVal)
                    .ThenByDescending(x => x.Card.ReviewCount)
                    .ThenBy(x => x.Card.Name, StringComparer.CurrentCultureIgnoreCase),
                "recruiting_desc" => enriched
                    .OrderByDescending(x => x.Card.OpenRecruitingCohortCount)
                    .ThenByDescending(x => x.Card.RecruitingCohortCount)
                    .ThenByDescending(x => x.AvgRatingVal)
                    .ThenByDescending(x => x.ExpVal)
                    .ThenBy(x => x.Card.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => enriched
                    .OrderByDescending(x => x.MatchRank)
                    .ThenByDescending(x => x.Card.MatchingRecruitingCohortCount)
                    .ThenByDescending(x => x.Card.OpenRecruitingCohortCount)
                    .ThenByDescending(x => x.AvgRatingVal)
                    .ThenByDescending(x => x.Card.ReviewCount)
                    .ThenByDescending(x => x.ExpVal)
                    .ThenBy(x => x.Card.Name, StringComparer.CurrentCultureIgnoreCase),
            };

            var sortedCards = ordered.Select(x => x.Card).ToList();
            var totalCount = sortedCards.Count;
            var pagedItems = sortedCards
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResult = new PagedResult<PublicMentorCardResponse>
            {
                Items = pagedItems,
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
            var completedCohortCount = unitOfWork.StudyCohorts == null
                ? 0
                : await unitOfWork.StudyCohorts.Query()
                    .AsNoTracking()
                    .CountAsync(
                        c => c.MentorId == mentor.Id
                            && (c.Status == "Completed"
                                || (c.Status == "Published"
                                    && c.Enrollments.Any(e =>
                                        (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                                        && e.Status != "Cancelled"
                                        && e.Status != "Transferred"
                                        && e.Status != "PaymentReview")
                                    && c.Enrollments
                                        .Where(e =>
                                            (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                                            && (e.Status == "Ready" || e.Status == "Active" || e.Status == "Completed"))
                                        .All(e => e.Status == "Completed"))),
                        cancellationToken);
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
                IsBookable = true,
                CompletedCohortCount = completedCohortCount
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

    private async Task<Dictionary<int, (double AverageRating, int ReviewCount)>> LoadPublicReviewSummariesAsync(
        IReadOnlyCollection<int> mentorIds, CancellationToken ct)
    {
        if (mentorIds.Count == 0 || unitOfWork.CohortMentorReviews == null)
            return [];

        var rows = await unitOfWork.CohortMentorReviews.Query().AsNoTracking()
            .Where(r => mentorIds.Contains(r.MentorId) && !r.IsHidden)
            .GroupBy(r => r.MentorId)
            .Select(g => new
            {
                MentorId = g.Key,
                Average = g.Average(r => (double)r.Rating),
                Count = g.Count(),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.MentorId, x => (Math.Round(x.Average, 2), x.Count));
    }

    private async Task<Dictionary<int, RecruitingCohortMatchSummary>> LoadRecruitingCohortsByMentorAsync(
        IReadOnlyCollection<int> mentorIds,
        string? normalizedTeachingLevel,
        HashSet<int> requestedWeekdays,
        int? fromMinute,
        int? toMinute,
        CancellationToken ct)
    {
        var result = new Dictionary<int, RecruitingCohortMatchSummary>();
        if (mentorIds.Count == 0 || unitOfWork.StudyCohorts == null)
            return result;

        var now = DateTime.UtcNow;
        var rows = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Include(x => x.Plan).ThenInclude(x => x.CertificateLevel)
            .Include(x => x.GroupPolicy)
            .Include(x => x.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
            .Include(x => x.Enrollments)
            .AsSplitQuery()
            .Where(x => mentorIds.Contains(x.MentorId)
                && x.Status == "Published"
                && x.StartsAt > now
                && x.Plan.IsPublished)
            .ToListAsync(ct);

        foreach (var group in rows.GroupBy(c => c.MentorId))
        {
            var totalRecruiting = 0;
            var openRecruiting = 0;
            var matchingRecruiting = 0;
            var matchingOpen = 0;
            var hasLevelRecruiting = false;

            foreach (var cohort in group)
            {
                var approvedSessions = cohort.Sessions
                    .Where(s => s.Availability != null
                        && s.Availability.IsApproved
                        && !s.Availability.IsDeleted
                        && s.Availability.RejectedAt == null)
                    .OrderBy(s => s.Availability.StartTime)
                    .ToList();

                var firstSlot = approvedSessions.FirstOrDefault();
                var cutoffUtc = cohort.EnrollmentClosesAtUtc
                    ?? (firstSlot is null ? cohort.StartsAt : firstSlot.Availability.StartTime);

                if (cutoffUtc <= now) continue;
                if (firstSlot is not null && firstSlot.Availability.StartTime <= now) continue;

                var effectiveCapacity = cohort.CapacitySnapshot is int snapCap && snapCap > 0 ? snapCap : cohort.Capacity;
                var confirmedIds = cohort.Enrollments
                    .Where(e => e.PaymentStatus is "Paid" or "PartiallyRefunded"
                        && e.Status is "Ready" or "Active" or "Completed")
                    .Select(e => e.LearnerId)
                    .ToHashSet();
                var pendingHoldCount = cohort.Enrollments
                    .Where(e => e.PaymentStatus == "Pending"
                        && e.Status == "PendingPayment"
                        && e.PaymentExpiresAt > now
                        && !confirmedIds.Contains(e.LearnerId))
                    .Select(e => e.LearnerId)
                    .Distinct()
                    .Count();
                var seatsAvailable = Math.Max(0, effectiveCapacity - confirmedIds.Count - pendingHoldCount);
                var isFull = effectiveCapacity > 0 && confirmedIds.Count >= effectiveCapacity;
                var isOpenForEnroll = !isFull && seatsAvailable > 0;

                totalRecruiting++;
                if (isOpenForEnroll) openRecruiting++;

                var levelCode = cohort.Plan?.CertificateLevel?.Code?.Trim().ToUpperInvariant();
                var levelOk = string.IsNullOrEmpty(normalizedTeachingLevel)
                    || string.Equals(levelCode, normalizedTeachingLevel, StringComparison.OrdinalIgnoreCase);
                if (levelOk && !string.IsNullOrEmpty(normalizedTeachingLevel))
                    hasLevelRecruiting = true;

                var scheduleCriteriaPresent = requestedWeekdays.Count > 0 || fromMinute.HasValue || toMinute.HasValue;
                var scheduleOk = true;
                if (scheduleCriteriaPresent)
                {
                    if (approvedSessions.Count == 0)
                    {
                        scheduleOk = false;
                    }
                    else
                    {
                        // Đối chiếu TOÀN BỘ lịch học bắt buộc của lớp theo giờ VN (không chỉ một buổi đại diện).
                        foreach (var sess in approvedSessions)
                        {
                            var localStart = ToBusinessTime(sess.Availability.StartTime);
                            var localEnd = ToBusinessTime(sess.Availability.EndTime);
                            var wd = (int)localStart.DayOfWeek;
                            var startMin = localStart.Hour * 60 + localStart.Minute;
                            var endMin = localEnd.Hour * 60 + localEnd.Minute;

                            if (requestedWeekdays.Count > 0 && !requestedWeekdays.Contains(wd))
                            {
                                scheduleOk = false;
                                break;
                            }
                            if (fromMinute.HasValue && startMin < fromMinute.Value)
                            {
                                scheduleOk = false;
                                break;
                            }
                            if (toMinute.HasValue && endMin > toMinute.Value)
                            {
                                scheduleOk = false;
                                break;
                            }
                        }
                    }
                }

                if (levelOk && scheduleOk)
                {
                    matchingRecruiting++;
                    if (isOpenForEnroll) matchingOpen++;
                }
            }

            result[group.Key] = new RecruitingCohortMatchSummary(
                totalRecruiting,
                openRecruiting,
                matchingRecruiting,
                matchingOpen,
                hasLevelRecruiting);
        }

        return result;
    }

    private async Task<Dictionary<int, List<string>>> LoadActiveTeachingLevelsAsync(
        IReadOnlyCollection<int> mentorIds, CancellationToken ct)
    {
        var result = new Dictionary<int, List<string>>();
        if (mentorIds.Count == 0)
            return result;

        var activeByMentor = new Dictionary<int, HashSet<string>>();
        var revokedByMentor = new Dictionary<int, HashSet<string>>();

        void AddActive(int mentorId, string? rawCode)
        {
            if (!TryNormalizeSupportedLevel(rawCode, out var normalized)) return;
            if (!activeByMentor.TryGetValue(mentorId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                activeByMentor[mentorId] = set;
            }
            set.Add(normalized);
        }

        if (unitOfWork.MentorTeachingQualifications != null)
        {
            var qualRows = await unitOfWork.MentorTeachingQualifications.Query().AsNoTracking()
                .Include(q => q.CertificateLevel)
                .Where(q => mentorIds.Contains(q.MentorUserId))
                .ToListAsync(ct);

            foreach (var row in qualRows)
            {
                var code = row.CertificateLevel?.Code ?? ResolveLevelCode(row.CertificateLevelId);
                if (!TryNormalizeSupportedLevel(code, out var normalized)) continue;

                if (row.IsActive && row.RevokedAtUtc == null)
                {
                    AddActive(row.MentorUserId, normalized);
                }
                else if (row.RevokedAtUtc != null && !row.IsActive)
                {
                    if (!revokedByMentor.TryGetValue(row.MentorUserId, out var rset))
                    {
                        rset = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        revokedByMentor[row.MentorUserId] = rset;
                    }
                    rset.Add(normalized);
                }
            }
        }

        // Đồng bộ từ nguồn lớp học thực tế đã được trung tâm phê duyệt (StudyCohorts Published/Completed & Approved proposals)
        // để đảm bảo Giáo viên đã có lớp được duyệt luôn phản ánh đúng trình độ giảng dạy thực tế (không suy diễn từ chứng chỉ JLPT).
        if (unitOfWork.StudyCohorts != null)
        {
            var cohortLevelRows = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .Where(c => mentorIds.Contains(c.MentorId) && (c.Status == "Published" || c.Status == "Completed"))
                .Select(c => new
                {
                    c.MentorId,
                    LevelCode = c.Plan != null && c.Plan.CertificateLevel != null ? c.Plan.CertificateLevel.Code : null,
                })
                .Distinct()
                .ToListAsync(ct);

            foreach (var row in cohortLevelRows)
                AddActive(row.MentorId, row.LevelCode);
        }

        foreach (var mentorId in mentorIds)
        {
            activeByMentor.TryGetValue(mentorId, out var activeSet);
            revokedByMentor.TryGetValue(mentorId, out var revokedSet);

            var sorted = (activeSet ?? [])
                .Where(code => revokedSet is null || !revokedSet.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(LevelSortRank)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result[mentorId] = sorted;
        }

        return result;
    }

    private static int LevelSortRank(string code) => code.ToUpperInvariant() switch
    {
        "N5" => 1,
        "N4" => 2,
        "N3" => 3,
        "N2" => 4,
        "N1" => 5,
        _ => 99
    };

    private static bool TryParseWeekdaysFilter(string? raw, out HashSet<int> weekdays)
    {
        weekdays = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (!int.TryParse(token, out var day) || day < 0 || day > 6)
                return false;
            weekdays.Add(day);
        }
        return true;
    }

    private static bool TryParseClockMinutes(string? raw, out int? minutes)
    {
        minutes = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!TimeSpan.TryParseExact(raw.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var ts)
            && !TimeSpan.TryParseExact(raw.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out ts))
        {
            return false;
        }
        minutes = (int)ts.TotalMinutes;
        return minutes is >= 0 and <= 24 * 60;
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
        }
    }

    private static DateTime ToBusinessTime(DateTime utcOrUnspecified)
    {
        var utc = utcOrUnspecified.Kind == DateTimeKind.Utc
            ? utcOrUnspecified
            : DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone);
    }

    private static bool TryNormalizeSupportedLevel(string? rawLevel, out string normalizedLevel)
    {
        normalizedLevel = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLevel))
            return false;

        var candidate = rawLevel.Trim().ToUpperInvariant();
        if (candidate is "N5" or "N4" or "N3" or "N2" or "N1")
        {
            normalizedLevel = candidate;
            return true;
        }
        return false;
    }

    private static string? ResolveLevelCode(int? certificateLevelId) => certificateLevelId switch
    {
        1 => "N5",
        2 => "N4",
        3 => "N3",
        4 => "N2",
        5 => "N1",
        _ => null
    };
}
