using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorDiscovery;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorDiscoveryService(
    IUnitOfWork unitOfWork,
    ILogger<MentorDiscoveryService> logger) : IMentorDiscoveryService
{
    public async Task<ApiResponse<PagedResult<PublicMentorCardResponse>>> SearchMentorsAsync(
        MentorDiscoveryFilterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            int? requiredTeachingLevelId = null;
            if (!string.IsNullOrWhiteSpace(request.TeachingJlptLevel))
            {
                if (!TryNormalizeSupportedLevel(request.TeachingJlptLevel, out var normalizedTeachingLevel))
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

    private static bool TryNormalizeSupportedLevel(string? rawLevel, out string normalizedLevel)
    {
        normalizedLevel = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLevel))
            return false;

        var candidate = rawLevel.Trim().ToUpperInvariant();
        if (candidate is "N5" or "N4" or "N3" or "N2")
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
        _ => null
    };

    private async Task<int> ResolveCertificateLevelIdAsync(string normalizedLevel, CancellationToken ct)
    {
        if (unitOfWork.CertificateLevels != null)
        {
            var certLevel = await unitOfWork.CertificateLevels.Query()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code.ToUpper() == normalizedLevel, ct);
            if (certLevel != null)
                return certLevel.Id;
        }
        return normalizedLevel switch
        {
            "N5" => 1,
            "N4" => 2,
            "N3" => 3,
            "N2" => 4,
            _ => 0
        };
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
                .Select(x => x.CertificateLevel?.Code ?? ResolveLevelCode(x.CertificateLevelId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            result[group.Key] = levels;
        }

        return result;
    }
}
