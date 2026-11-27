using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

public sealed class MentorTeachingProposalService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IMentorGroupPolicyService groupPolicyService,
    IMentorTeachingConflictService teachingConflictService,
    ISidebarCounterNotifier? sidebarNotifier = null)
    : IMentorTeachingProposalService
{
    private const int DefaultOperatingStartMinute = 7 * 60;   // 07:00 VN
    private const int DefaultOperatingEndMinute = 22 * 60;    // 22:00 VN
    private const int DefaultGridStepMinutes = 15;
    private const int MaxOptionalAttachments = 5;
    private const long MaxAttachmentBytes = 25 * 1024 * 1024;
    private static readonly System.Text.RegularExpressions.Regex LocalProposalAttachmentRegex = new(
        @"^/api/FileStorage/local/mentor-proposals/[a-zA-Z0-9._-]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".xlsx", ".xls", ".csv", ".txt"
    };
    private static readonly HashSet<string> SupportedClassJlptCodes =
        new(["N5", "N4", "N3", "N2"], StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<TimeZoneInfo> BusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));

    private async Task<(MentorGroupPolicy? Policy, ApiResponse<T>? Error)> RequireEffectivePolicyAsync<T>(DateTime utc, CancellationToken ct)
    {
        var policy = await groupPolicyService.GetEffectiveAsync(utc, ct);
        if (policy is not null)
            return (policy, null);
        return (null, ApiResponse<T>.Fail(
            "Hệ thống chưa có chính sách lớp học nhóm đang có hiệu lực. Vui lòng liên hệ Quản trị viên.",
            HttpStatusCode.ServiceUnavailable,
            "GROUP_POLICY_UNAVAILABLE"));
    }

    public async Task<ApiResponse<MentorTeachingProposalContextResponse>> GetMentorContextAsync(CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<MentorTeachingProposalContextResponse>.Fail(
                "Chỉ Giáo viên mới có thể truy cập không gian đề xuất lịch dạy.",
                HttpStatusCode.Forbidden,
                "FORBIDDEN_ROLE");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<MentorTeachingProposalContextResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var policyDto = MapPolicy(policy!);

        var mentor = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Id == claims.Id && !u.IsDeleted, ct);
        if (mentor is null)
            return ApiResponse<MentorTeachingProposalContextResponse>.NotFound("Không tìm thấy tài khoản Giáo viên.");

        var existingQualRows = await unitOfWork.MentorTeachingQualifications.Query().AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Where(q => q.MentorUserId == claims.Id && q.IsActive && q.RevokedAtUtc == null)
            .Select(q => new { q.CertificateLevelId, Code = q.CertificateLevel.Code, q.VerifiedAtUtc })
            .ToListAsync(ct);
        var existingQualMap = existingQualRows.ToDictionary(q => q.CertificateLevelId, q => (DateTime?)q.VerifiedAtUtc);

        var mentorVerified = mentor.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved;
        var profileJlptCode = mentor.MentorProfile?.JlptLevel?.Trim();
        var highestVerifiedRank = ResolveHighestVerifiedRank(
            mentor.MentorProfile,
            existingQualRows.Select(q => q.Code));
        var highestVerifiedCode = MentorSlotPolicy.GetJlptCodeFromRank(highestVerifiedRank);

        var activeLevelsRaw = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Include(l => l.CertificateType)
            .Where(l => !l.IsDeleted && l.CertificateType != null && !l.CertificateType.IsDeleted && l.CertificateType.IsActive)
            .OrderBy(l => l.CertificateType.SortOrder)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Code, l.Description })
            .ToListAsync(ct);

        var (levelBlockingMap, activeCountByLevel, totalActiveCount, maxTotalActiveClasses) =
            await BuildLevelBlockingMapAsync(claims.Id, excludeProposalIds: null, policy!, nowUtc, ct);
        var totalLimitReached = maxTotalActiveClasses > 0 && totalActiveCount >= maxTotalActiveClasses;

        var availableLevels = activeLevelsRaw
            .Where(l => SupportedClassJlptCodes.Contains((l.Code ?? string.Empty).Trim()))
            .Select(l =>
            {
                levelBlockingMap.TryGetValue(l.Id, out var block);
                var isQualified = mentorVerified && MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, l.Code);
                string? qualificationReason = null;
                if (!mentorVerified || highestVerifiedRank <= 0)
                {
                    qualificationReason = "Chưa có chứng chỉ chuyên môn được xác minh.";
                }
                else if (!isQualified)
                {
                    qualificationReason = "Không đủ trình độ chuyên môn.";
                }

                var activeCount = activeCountByLevel.GetValueOrDefault(l.Id);
                var maxActiveForLevel = policy!.GetMaxActiveClassesForLevel(l.Id, l.Code);
                var levelRemaining = Math.Max(0, maxActiveForLevel - activeCount);
                var remainingSlots = !isQualified || maxActiveForLevel <= 0
                    ? 0
                    : totalLimitReached
                        ? 0
                        : maxTotalActiveClasses > 0
                            ? Math.Max(0, Math.Min(levelRemaining, maxTotalActiveClasses - totalActiveCount))
                            : levelRemaining;

                var isAvailable = isQualified && block is null && maxActiveForLevel > 0 && !totalLimitReached;
                var unavailableReason = !isQualified
                    ? qualificationReason
                    : FormatLevelBlockingReason(l.Code, block);

                return new QualifiedJlptLevelItem
                {
                    CertificateLevelId = l.Id,
                    Code = l.Code,
                    Name = string.IsNullOrWhiteSpace(l.Description) ? $"JLPT {l.Code}" : l.Description,
                    VerifiedAtUtc = existingQualMap.GetValueOrDefault(l.Id),
                    IsRecommendedByProfile = !string.IsNullOrWhiteSpace(highestVerifiedCode)
                        && string.Equals(l.Code, highestVerifiedCode, StringComparison.OrdinalIgnoreCase),
                    IsQualified = isQualified,
                    QualificationReason = qualificationReason,
                    IsAvailableForNewProposal = isAvailable,
                    UnavailableReason = unavailableReason,
                    BlockingStatus = !isQualified
                        ? (mentorVerified && highestVerifiedRank > 0 ? "InsufficientQualification" : "UnverifiedQualification")
                        : block?.Status,
                    BlockingProposalId = isQualified ? block?.ProposalId : null,
                    BlockingCohortId = isQualified ? block?.CohortId : null,
                    BlockingTitle = isQualified ? block?.Title : null,
                    ActiveCount = activeCount,
                    MaxActiveClasses = maxActiveForLevel,
                    RemainingSlots = remainingSlots,
                };
            })
            .ToList();

        var availableLevelIds = availableLevels.Select(l => l.CertificateLevelId).ToHashSet();

        var plans = await unitOfWork.StudyCoursePlans.Query().AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Where(p => p.SessionType == "Group" && availableLevelIds.Contains(p.CertificateLevelId))
            .OrderBy(p => p.CertificateLevel.SortOrder)
            .ThenBy(p => p.SessionsPerWeek)
            .ThenBy(p => p.Name)
            .Select(p => new EligibleCoursePlanItem
            {
                Id = p.Id,
                Name = p.Name,
                CertificateLevelId = p.CertificateLevelId,
                LevelCode = p.CertificateLevel.Code,
                SessionsPerWeek = p.SessionsPerWeek,
                SessionMinutes = p.SessionMinutes ?? policy.SessionMinutes,
                TotalSessions = p.TotalSessions ?? (policy.CourseDurationWeeks * p.SessionsPerWeek),
                Capacity = p.Capacity,
                Price = p.Price,
                IsPublished = p.IsPublished,
            })
            .ToListAsync(ct);

        var allProposals = await QueryProposalsWithIncludes()
            .Where(p => p.MentorId == claims.Id && p.Status != MentorTeachingProposalStatus.Withdrawn)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync(ct);

        // Mỗi thời điểm chỉ giữ tối đa 1 lịch Đang chọn (Draft) có ít nhất 1 buổi/tuần cho Giáo viên
        var draftSeen = false;
        var proposals = new List<MentorTeachingProposal>(allProposals.Count);
        foreach (var item in allProposals)
        {
            if (item.Status == MentorTeachingProposalStatus.Draft)
            {
                if (DeserializeSessions(item.WeeklySessionsJson).Count == 0) continue;
                if (draftSeen) continue;
                draftSeen = true;
            }
            proposals.Add(item);
        }

        var workload = await ComputeMentorWorkloadAsync(claims.Id, policy, nowUtc, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(proposals.Select(p => p.Id).ToList(), ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);

        var mappedProposals = proposals
            .Select(p => MapProposal(p, policy, qualCodes, linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false))
            .ToList();

        return ApiResponse<MentorTeachingProposalContextResponse>.Success(new MentorTeachingProposalContextResponse
        {
            MentorVerified = mentorVerified,
            MentorVerificationStatus = mentor.MentorProfile?.VerificationStatus.ToString(),
            MentorDeclaredJlptLevel = profileJlptCode,
            MentorVerifiedJlptLevel = highestVerifiedCode,
            Policy = policyDto,
            AvailableLevels = availableLevels,
            QualifiedLevels = availableLevels,
            CoursePlans = plans,
            Workload = workload,
            Proposals = mappedProposals,
        });
    }

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetMyProposalsAsync(string? status = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ Giáo viên mới có thể xem đề xuất của mình.", HttpStatusCode.Forbidden);

        var policy = (await groupPolicyService.GetEffectiveAsync(DateTime.UtcNow, ct));
        if (policy is null)
            return ApiResponse<List<TeachingProposalResponse>>.Fail(
                "Hệ thống chưa có chính sách lớp học nhóm đang có hiệu lực. Vui lòng liên hệ Quản trị viên.",
                HttpStatusCode.ServiceUnavailable,
                "GROUP_POLICY_UNAVAILABLE");
        var query = QueryProposalsWithIncludes()
            .Where(p => p.MentorId == claims.Id && p.Status != MentorTeachingProposalStatus.Withdrawn);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(p => p.Status == normalizedStatus);
        }

        var rows = await query.OrderByDescending(p => p.UpdatedAtUtc).ToListAsync(ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(rows.Select(p => p.Id).ToList(), ct);

        return ApiResponse<List<TeachingProposalResponse>>.Success(
            rows.Select(p => MapProposal(p, policy, qualCodes, linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false)).ToList());
    }

    public async Task<ApiResponse<TeachingProposalResponse>> GetMyProposalByIdAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể xem đề xuất này.", HttpStatusCode.Forbidden);

        var proposal = await QueryProposalsWithIncludes(includeEvents: true)
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id && p.Status != MentorTeachingProposalStatus.Withdrawn, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Thông tin đề xuất này không còn khả dụng hoặc đã được cập nhật.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var activePolicy = policy!;
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, activePolicy, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, activePolicy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<ProposalAttachmentDto>> GetAuthorizedAttachmentAsync(
        int proposalId, int attachmentIndex, bool isAdmin, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (isAdmin && claims.Role != Role.Admin)
            return ApiResponse<ProposalAttachmentDto>.Fail("Chỉ quản trị viên mới có thể truy cập tài liệu này.", HttpStatusCode.Forbidden);
        if (!isAdmin && claims.Role != Role.Mentor)
            return ApiResponse<ProposalAttachmentDto>.Fail("Chỉ Giáo viên mới có thể truy cập tài liệu này.", HttpStatusCode.Forbidden);

        var proposal = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Where(p => p.Id == proposalId)
            .Select(p => new { p.Id, p.MentorId, p.Status, p.OptionalAttachmentsJson })
            .FirstOrDefaultAsync(ct);

        if (proposal is null)
            return ApiResponse<ProposalAttachmentDto>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        if (!isAdmin && (proposal.MentorId != claims.Id || proposal.Status == MentorTeachingProposalStatus.Withdrawn))
            return ApiResponse<ProposalAttachmentDto>.Fail("Bạn không có quyền truy cập tài liệu của đề xuất này.", HttpStatusCode.Forbidden, "PROPOSAL_ATTACHMENT_FORBIDDEN");

        var attachments = DeserializeAttachments(proposal.OptionalAttachmentsJson);
        if (attachmentIndex < 0 || attachmentIndex >= attachments.Count)
            return ApiResponse<ProposalAttachmentDto>.NotFound("Không tìm thấy tài liệu đính kèm.", "PROPOSAL_ATTACHMENT_NOT_FOUND");

        return ApiResponse<ProposalAttachmentDto>.Success(attachments[attachmentIndex]);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> CreateDraftAsync(SaveTeachingProposalDraftRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể tạo phiên đăng ký mở lớp.", HttpStatusCode.Forbidden);

        request ??= new SaveTeachingProposalDraftRequest();
        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        var sanitizeErr = ValidateDraftFields(request, policy, out var normalizedProgram, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        var isCompletelyEmpty = normalizedSessions.Count == 0
            && !request.CertificateLevelId.HasValue
            && !request.ExpectedStartDate.HasValue
            && string.IsNullOrWhiteSpace(request.CourseTitle)
            && string.IsNullOrWhiteSpace(request.CourseObjective)
            && string.IsNullOrWhiteSpace(request.OptionalNotes)
            && normalizedAttachments.Count == 0;
        if (isCompletelyEmpty)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng chọn ít nhất một khung giờ hoặc nhập thông tin trước khi lưu phiên đăng ký.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_EMPTY_SELECTION");
        }

        if (normalizedSessions.Count == 0)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng chọn ít nhất 1 buổi dạy để lưu lịch đang chọn.",
                HttpStatusCode.BadRequest,
                "EMPTY_SELECTION_NOT_ALLOWED");
        }

        int createdId = 0;
        ApiResponse<TeachingProposalResponse>? failure = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var capacityErr = await CheckMentorNewRegistrationCapacityAsync(claims.Id, policy!, nowUtc, token);
                if (capacityErr.HasValue)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(capacityErr.Value.Message, HttpStatusCode.Conflict, capacityErr.Value.Code);
                    return;
                }

                var existingDrafts = await unitOfWork.MentorTeachingProposals.Query()
                    .Where(p => p.MentorId == claims.Id && p.Status == MentorTeachingProposalStatus.Draft)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync(token);

                if (existingDrafts.Count > 0)
                {
                    // Tái sử dụng duy nhất 1 phiên Draft hiện hành và dọn dẹp các bản Draft thừa nếu có
                    var activeDraft = existingDrafts[0];
                    activeDraft.CertificateLevelId = request.CertificateLevelId;
                    activeDraft.ProgramType = normalizedProgram;
                    activeDraft.CoursePlanId = request.CoursePlanId;
                    activeDraft.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                    activeDraft.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                    activeDraft.ExpectedStartDate = request.ExpectedStartDate;
                    activeDraft.CourseDurationWeeks = request.CourseDurationWeeks ?? policy.CourseDurationWeeks;
                    activeDraft.SessionDurationMinutes = request.SessionDurationMinutes ?? policy.SessionMinutes;
                    activeDraft.WeeklySessionsJson = JsonSerializer.Serialize(normalizedSessions, JsonOpts);
                    activeDraft.OptionalNotes = TrimOrNull(request.OptionalNotes, 2000);
                    activeDraft.OptionalAttachmentsJson = JsonSerializer.Serialize(normalizedAttachments, JsonOpts);
                    activeDraft.GroupPolicyId = policy.Id;
                    activeDraft.PolicyVersionSnapshot = policy.Version;
                    activeDraft.Revision++;
                    activeDraft.UpdatedAtUtc = nowUtc;
                    activeDraft.LastSavedAtUtc = nowUtc;
                    unitOfWork.MentorTeachingProposals.Update(activeDraft);

                    for (var i = 1; i < existingDrafts.Count; i++)
                    {
                        unitOfWork.MentorTeachingProposals.Remove(existingDrafts[i]);
                    }

                    await unitOfWork.SaveChangesAsync(token);
                    createdId = activeDraft.Id;
                    return;
                }

                var proposal = new MentorTeachingProposal
                {
                    MentorId = claims.Id,
                    CertificateLevelId = request.CertificateLevelId,
                    ProgramType = normalizedProgram,
                    CoursePlanId = request.CoursePlanId,
                    CourseTitle = TrimOrNull(request.CourseTitle, 200),
                    CourseObjective = TrimOrNull(request.CourseObjective, 1500),
                    ExpectedStartDate = request.ExpectedStartDate,
                    CourseDurationWeeks = request.CourseDurationWeeks ?? policy.CourseDurationWeeks,
                    SessionDurationMinutes = request.SessionDurationMinutes ?? policy.SessionMinutes,
                    WeeklySessionsJson = JsonSerializer.Serialize(normalizedSessions, JsonOpts),
                    OptionalNotes = TrimOrNull(request.OptionalNotes, 2000),
                    OptionalAttachmentsJson = JsonSerializer.Serialize(normalizedAttachments, JsonOpts),
                    Status = MentorTeachingProposalStatus.Draft,
                    Revision = 1,
                    GroupPolicyId = policy.Id,
                    PolicyVersionSnapshot = policy.Version,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc,
                    LastSavedAtUtc = nowUtc,
                };

                proposal.Events.Add(new MentorTeachingProposalEvent
                {
                    ActorUserId = claims.Id,
                    ActorRole = Role.Mentor.ToString(),
                    Action = "DraftCreated",
                    FromStatus = null,
                    ToStatus = MentorTeachingProposalStatus.Draft,
                    RevisionAfter = 1,
                    CreatedAtUtc = nowUtc,
                });

                await unitOfWork.MentorTeachingProposals.AddAsync(proposal, token);
                await unitOfWork.SaveChangesAsync(token);
                createdId = proposal.Id;
            }, ct);
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Không thể tạo phiên đăng ký do xung đột dữ liệu đồng thời. Vui lòng thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == createdId, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    public async Task<ApiResponse<TeachingProposalResponse>> UpdateDraftAsync(int id, SaveTeachingProposalDraftRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể lưu phiên đang chọn.", HttpStatusCode.Forbidden);

        if (request is null)
            return ApiResponse<TeachingProposalResponse>.Fail("Dữ liệu đăng ký không hợp lệ.");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var sanitizeErr = ValidateDraftFields(request, policy, out var normalizedProgram, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        ApiResponse<TeachingProposalResponse>? failure = null;
        var deletedEmptyDraft = false;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
                if (proposal is null)
                {
                    failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy lịch đang chọn.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.Draft)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Chỉ lịch đang chọn chưa gửi duyệt mới được cập nhật trực tiếp. Đề xuất cần bổ sung chỉ được cập nhật khi gửi lại xét duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != proposal.Revision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Lịch đang chọn đã được cập nhật ở tab hoặc thiết bị khác. Vui lòng đồng bộ dữ liệu mới nhất trước khi tiếp tục.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                // Zero Selection Invariant: không bao giờ duy trì bản ghi Draft 0 buổi/tuần trong DB
                if (proposal.Status == MentorTeachingProposalStatus.Draft && normalizedSessions.Count == 0)
                {
                    unitOfWork.MentorTeachingProposals.Remove(proposal);
                    await unitOfWork.SaveChangesAsync(token);
                    deletedEmptyDraft = true;
                    return;
                }

                var capacityErr = await CheckMentorNewRegistrationCapacityAsync(claims.Id, policy!, nowUtc, token);
                if (capacityErr.HasValue)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(capacityErr.Value.Message, HttpStatusCode.Conflict, capacityErr.Value.Code);
                    return;
                }

                proposal.CertificateLevelId = request.CertificateLevelId;
                proposal.ProgramType = normalizedProgram;
                proposal.CoursePlanId = request.CoursePlanId;
                proposal.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                proposal.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                proposal.ExpectedStartDate = request.ExpectedStartDate;
                proposal.CourseDurationWeeks = request.CourseDurationWeeks ?? policy.CourseDurationWeeks;
                proposal.SessionDurationMinutes = request.SessionDurationMinutes ?? policy.SessionMinutes;
                proposal.WeeklySessionsJson = JsonSerializer.Serialize(normalizedSessions, JsonOpts);
                proposal.OptionalNotes = TrimOrNull(request.OptionalNotes, 2000);
                proposal.OptionalAttachmentsJson = JsonSerializer.Serialize(normalizedAttachments, JsonOpts);
                proposal.GroupPolicyId = policy.Id;
                proposal.PolicyVersionSnapshot = policy.Version;
                proposal.Revision++;
                proposal.UpdatedAtUtc = nowUtc;
                proposal.LastSavedAtUtc = nowUtc;

                unitOfWork.MentorTeachingProposals.Update(proposal);
                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Phát hiện xung đột cập nhật đồng thời. Vui lòng đồng bộ dữ liệu mới nhất.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Dữ liệu đề xuất vừa thay đổi đồng thời. Vui lòng thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }

        if (failure is not null)
            return failure;

        if (deletedEmptyDraft)
        {
            return ApiResponse<TeachingProposalResponse>.Success(new TeachingProposalResponse
            {
                Id = 0,
                Status = MentorTeachingProposalStatus.Draft,
                CourseDurationWeeks = policy.CourseDurationWeeks,
                SessionDurationMinutes = policy.SessionMinutes,
                WeeklySessions = [],
            });
        }

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Success(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    public async Task<ApiResponse<bool>> DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<bool>.Fail("Chỉ Giáo viên mới có thể xóa phiên đang chọn.", HttpStatusCode.Forbidden);

        ApiResponse<bool>? failure = null;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
                if (proposal is null)
                {
                    // Idempotent cleanup: đã bị xóa trước đó hoặc ở tab khác
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.Draft)
                {
                    failure = ApiResponse<bool>.Fail(
                        "Chỉ có thể xóa lịch đang chọn chưa gửi duyệt. Với đề xuất đang chờ duyệt, hãy chọn Hủy đề xuất.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                unitOfWork.MentorTeachingProposals.Remove(proposal);
                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<bool>.Fail("Phiên đang chọn vừa thay đổi. Vui lòng tải lại.", HttpStatusCode.Conflict, "PROPOSAL_REVISION_CONFLICT");
        }

        return failure ?? ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<TeachingProposalValidationResponse>> ValidateDraftAsync(
        ValidateTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalValidationResponse>.Fail("Chỉ Giáo viên mới có thể kiểm tra đề xuất.", HttpStatusCode.Forbidden);

        request ??= new ValidateTeachingProposalRequest();
        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalValidationResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var activePolicy = policy!;

        var snapshot = new ProposalCandidate(
            ProposalId: request.ProposalId,
            MentorId: claims.Id,
            CertificateLevelId: request.CertificateLevelId,
            ProgramType: NormalizeProgramType(request.ProgramType),
            CoursePlanId: request.CoursePlanId,
            CourseObjective: TrimOrNull(request.CourseObjective, 1500),
            ExpectedStartDate: request.ExpectedStartDate,
            CourseDurationWeeks: request.CourseDurationWeeks ?? activePolicy.CourseDurationWeeks,
            SessionDurationMinutes: request.SessionDurationMinutes ?? activePolicy.SessionMinutes,
            WeeklySessions: NormalizeWeeklySessions(request.WeeklySessions, request.SessionDurationMinutes ?? activePolicy.SessionMinutes),
            OptionalAttachments: []);

        var validation = await EvaluateProposalAsync(snapshot, activePolicy, nowUtc, ct);
        return ApiResponse<TeachingProposalValidationResponse>.Success(validation);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> SubmitProposalAsync(
        int id, SubmitTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể gửi duyệt đề xuất.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Thiếu khóa chống gửi trùng (idempotencyKey).",
                HttpStatusCode.BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
            return ApiResponse<TeachingProposalResponse>.Fail("Khóa idempotency vượt quá 128 ký tự.", HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_INVALID");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        ApiResponse<TeachingProposalResponse>? failure = null;
        int? resultId = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                // Kiểm tra idempotency trước tiên để ngăn double-submit khi mạng chậm/retry
                var existingByKey = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .FirstOrDefaultAsync(p => p.MentorId == claims.Id && p.SubmissionIdempotencyKey == idempotencyKey, token);
                if (existingByKey is not null)
                {
                    if (existingByKey.Id == id)
                    {
                        resultId = existingByKey.Id;
                        return;
                    }
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Khóa gửi duyệt đã được sử dụng cho một đề xuất khác.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IDEMPOTENCY_CONFLICT");
                    return;
                }

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
                if (proposal is null)
                {
                    failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                if (proposal.Status is MentorTeachingProposalStatus.PendingReview or MentorTeachingProposalStatus.Approved)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất này đã được gửi duyệt hoặc đã được phê duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_ALREADY_SUBMITTED");
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.Draft && proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Chỉ có thể gửi duyệt phiên đang chọn hoặc đề xuất cần bổ sung.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (proposal.Revision != request.ExpectedRevision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Dữ liệu đang chọn đã thay đổi trên phiên làm việc khác. Vui lòng đồng bộ bản mới nhất trước khi gửi duyệt.",
                        HttpStatusCode.Conflict,
                        "STALE_PROPOSAL_DATA");
                    return;
                }

                var candidate = CandidateFromEntity(proposal);
                var validation = await EvaluateProposalAsync(candidate, policy, nowUtc, token);
                if (!validation.CanSubmit)
                {
                    failure = BuildSubmitValidationFailure(validation);
                    return;
                }

                await ApplySubmissionTransitionAndNotificationsAsync(
                    proposal,
                    claims.Id,
                    idempotencyKey,
                    candidate.WeeklySessions.Count,
                    policy,
                    nowUtc,
                    scheduleChangedOnResubmit: false,
                    token);

                await unitOfWork.SaveChangesAsync(token);
                resultId = proposal.Id;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Phát hiện xung đột cập nhật đồng thời khi gửi đề xuất. Vui lòng đồng bộ lại và thử lại.",
                HttpStatusCode.Conflict,
                "STALE_PROPOSAL_DATA");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã được gửi hoặc bị xung đột đồng thời với đề xuất khác.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        return await BuildSubmittedProposalSuccessResponseAsync(resultId!.Value, claims.Id, policy, nowUtc, ct);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ResubmitProposalAsync(
        int id, ResubmitTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể gửi lại đề xuất mở lớp.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Thiếu khóa chống gửi trùng (idempotencyKey).",
                HttpStatusCode.BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
            return ApiResponse<TeachingProposalResponse>.Fail("Khóa idempotency vượt quá 128 ký tự.", HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_INVALID");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var draftCheck = new SaveTeachingProposalDraftRequest
        {
            CertificateLevelId = request.CertificateLevelId,
            ProgramType = request.ProgramType,
            CoursePlanId = request.CoursePlanId,
            CourseTitle = request.CourseTitle,
            CourseObjective = request.CourseObjective,
            ExpectedStartDate = request.ExpectedStartDate,
            CourseDurationWeeks = request.CourseDurationWeeks,
            SessionDurationMinutes = request.SessionDurationMinutes,
            WeeklySessions = request.WeeklySessions,
            OptionalNotes = request.OptionalNotes,
            OptionalAttachments = request.OptionalAttachments,
            ExpectedRevision = request.ExpectedRevision,
        };

        var sanitizeErr = ValidateDraftFields(draftCheck, policy, out var normalizedProgram, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        var allowedCounts = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv);
        var minPerWeek = allowedCounts.Min();
        if (normalizedSessions.Count < minPerWeek)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                $"Mỗi đề xuất mở lớp cần tối thiểu {minPerWeek} buổi/tuần (hiện có {normalizedSessions.Count} buổi/tuần).",
                HttpStatusCode.UnprocessableEntity,
                "PROPOSAL_MIN_SESSIONS_NOT_MET");
        }

        ApiResponse<TeachingProposalResponse>? failure = null;
        int? resultId = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var existingByKey = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .FirstOrDefaultAsync(p => p.MentorId == claims.Id && p.SubmissionIdempotencyKey == idempotencyKey, token);
                if (existingByKey is not null)
                {
                    if (existingByKey.Id == id)
                    {
                        resultId = existingByKey.Id;
                        return;
                    }
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Khóa gửi duyệt đã được sử dụng cho một đề xuất khác.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IDEMPOTENCY_CONFLICT");
                    return;
                }

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
                if (proposal is null)
                {
                    failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                if (proposal.Status is MentorTeachingProposalStatus.PendingReview or MentorTeachingProposalStatus.Approved)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất này đã được gửi duyệt hoặc đã được phê duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_ALREADY_SUBMITTED");
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Chỉ đề xuất đang ở trạng thái Cần bổ sung mới có thể gửi lại xét duyệt qua quy trình này.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (proposal.Revision != request.ExpectedRevision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất đã thay đổi trên phiên làm việc khác. Vui lòng đồng bộ bản mới nhất trước khi gửi lại.",
                        HttpStatusCode.Conflict,
                        "STALE_PROPOSAL_DATA");
                    return;
                }

                var resolvedLevelId = request.CertificateLevelId ?? proposal.CertificateLevelId;
                if (proposal.CertificateLevelId.HasValue && resolvedLevelId.HasValue && resolvedLevelId.Value != proposal.CertificateLevelId.Value)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Không thể thay đổi trình độ JLPT của đề xuất đang ở trạng thái Cần bổ sung.",
                        HttpStatusCode.UnprocessableEntity,
                        "PROPOSAL_LEVEL_LOCKED_ON_REVISION");
                    return;
                }

                var resolvedWeeks = policy.CourseDurationWeeks;
                var resolvedMinutes = policy.SessionMinutes;
                var candidate = new ProposalCandidate(
                    ProposalId: proposal.Id,
                    MentorId: claims.Id,
                    CertificateLevelId: resolvedLevelId,
                    ProgramType: normalizedProgram,
                    CoursePlanId: request.CoursePlanId,
                    CourseObjective: TrimOrNull(request.CourseObjective, 1500),
                    ExpectedStartDate: request.ExpectedStartDate,
                    CourseDurationWeeks: resolvedWeeks,
                    SessionDurationMinutes: resolvedMinutes,
                    WeeklySessions: normalizedSessions,
                    OptionalAttachments: normalizedAttachments);

                var validation = await EvaluateProposalAsync(candidate, policy, nowUtc, token);
                if (!validation.CanSubmit)
                {
                    // Atomicity guarantee: NEVER mutate official proposal row/schedule if validation fails
                    failure = BuildSubmitValidationFailure(validation);
                    return;
                }

                var previousSessions = DeserializeSessions(proposal.WeeklySessionsJson);
                var newWeeklyJson = JsonSerializer.Serialize(normalizedSessions, JsonOpts);
                var scheduleChanged = previousSessions.Count != normalizedSessions.Count
                    || !previousSessions.SequenceEqual(normalizedSessions, WeeklySlotEqualityComparer.Instance)
                    || proposal.ExpectedStartDate != request.ExpectedStartDate;

                proposal.CertificateLevelId = resolvedLevelId;
                proposal.ProgramType = normalizedProgram;
                proposal.CoursePlanId = request.CoursePlanId;
                proposal.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                proposal.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                proposal.ExpectedStartDate = request.ExpectedStartDate;
                proposal.CourseDurationWeeks = resolvedWeeks;
                proposal.SessionDurationMinutes = resolvedMinutes;
                proposal.WeeklySessionsJson = newWeeklyJson;
                proposal.OptionalNotes = TrimOrNull(request.OptionalNotes, 2000);
                proposal.OptionalAttachmentsJson = JsonSerializer.Serialize(normalizedAttachments, JsonOpts);

                await ApplySubmissionTransitionAndNotificationsAsync(
                    proposal,
                    claims.Id,
                    idempotencyKey,
                    normalizedSessions.Count,
                    policy,
                    nowUtc,
                    scheduleChangedOnResubmit: scheduleChanged,
                    token);

                await unitOfWork.SaveChangesAsync(token);
                resultId = proposal.Id;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Phát hiện xung đột cập nhật đồng thời khi gửi lại đề xuất. Vui lòng đồng bộ và thử lại.",
                HttpStatusCode.Conflict,
                "STALE_PROPOSAL_DATA");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã được gửi hoặc bị xung đột đồng thời với đề xuất khác.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        return await BuildSubmittedProposalSuccessResponseAsync(resultId!.Value, claims.Id, policy, nowUtc, ct);
    }

    private static ApiResponse<TeachingProposalResponse> BuildSubmitValidationFailure(TeachingProposalValidationResponse validation)
    {
        var blockingErrors = validation.Issues.Where(i => i.Severity == "Error").ToList();
        var firstConflict = blockingErrors.FirstOrDefault(i => IsConflictOrCapacityCode(i.Code));
        var chosen = firstConflict ?? blockingErrors.FirstOrDefault();
        var httpStatus = firstConflict is not null ? HttpStatusCode.Conflict : HttpStatusCode.UnprocessableEntity;
        return ApiResponse<TeachingProposalResponse>.Fail(
            chosen?.Message ?? "Đề xuất chưa đáp ứng đủ quy định lịch dạy.",
            httpStatus,
            chosen?.Code ?? "PROPOSAL_VALIDATION_FAILED",
            errors: blockingErrors.Select(i => i.Message).Distinct().ToList());
    }

    private async Task ApplySubmissionTransitionAndNotificationsAsync(
        MentorTeachingProposal proposal,
        int mentorId,
        string idempotencyKey,
        int weeklySessionCount,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        bool scheduleChangedOnResubmit,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(proposal.CourseTitle) && proposal.CertificateLevelId.HasValue)
        {
            var lvlCode = await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Where(l => l.Id == proposal.CertificateLevelId.Value)
                .Select(l => l.Code)
                .FirstOrDefaultAsync(token);
            proposal.CourseTitle = $"JLPT {lvlCode ?? "Group"} ({weeklySessionCount} buổi/tuần)";
        }

        var fromStatus = proposal.Status;
        proposal.Status = MentorTeachingProposalStatus.PendingReview;
        proposal.SubmissionIdempotencyKey = idempotencyKey;
        proposal.SubmittedAtUtc = nowUtc;
        proposal.ReviewedAtUtc = null;
        proposal.ReviewedByAdminId = null;
        proposal.ReviewReason = null;
        proposal.GroupPolicyId = policy.Id;
        proposal.PolicyVersionSnapshot = policy.Version;
        proposal.Revision++;
        proposal.UpdatedAtUtc = nowUtc;
        proposal.LastSavedAtUtc = nowUtc;

        unitOfWork.MentorTeachingProposals.Update(proposal);
        await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
        {
            ProposalId = proposal.Id,
            ActorUserId = mentorId,
            ActorRole = Role.Mentor.ToString(),
            Action = fromStatus == MentorTeachingProposalStatus.ChangesRequested ? "Resubmitted" : "Submitted",
            FromStatus = fromStatus,
            ToStatus = MentorTeachingProposalStatus.PendingReview,
            RevisionAfter = proposal.Revision,
            Reason = scheduleChangedOnResubmit ? "Giáo viên đã cập nhật lịch giảng dạy và thông tin đề xuất." : null,
            SnapshotJson = BuildSnapshotJson(proposal),
            CreatedAtUtc = nowUtc,
        }, token);

        var adminIds = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Role == Role.Admin && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(token);

        var mentorUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == mentorId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(token);
        var mentorName = mentorUser is null ? $"#{mentorId}" : $"{mentorUser.FirstName} {mentorUser.LastName}".Trim();
        var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất #{proposal.Id}" : proposal.CourseTitle;
        var levelCode = proposal.CertificateLevelId.HasValue
            ? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Where(l => l.Id == proposal.CertificateLevelId.Value)
                .Select(l => l.Code)
                .FirstOrDefaultAsync(token)
            : null;
        var isResubmission = fromStatus == MentorTeachingProposalStatus.ChangesRequested;

        var adminNotifTitle = isResubmission
            ? (string.IsNullOrWhiteSpace(levelCode)
                ? "Giáo viên đã bổ sung đề xuất mở lớp JLPT"
                : $"Giáo viên đã bổ sung đề xuất JLPT {levelCode}")
            : (string.IsNullOrWhiteSpace(levelCode)
                ? "Có đề xuất mở lớp JLPT mới chờ duyệt"
                : $"Đề xuất mở lớp JLPT {levelCode} mới chờ duyệt");

        var scheduleSuffix = scheduleChangedOnResubmit ? " (bao gồm cập nhật lịch dạy)" : string.Empty;
        var adminNotifMessage = isResubmission
            ? $"Giáo viên {mentorName} đã bổ sung thông tin{scheduleSuffix} và gửi lại đề xuất mở lớp \"{titleLabel}\" ({weeklySessionCount} buổi/tuần × {proposal.CourseDurationWeeks} tuần) chờ Quản trị viên xét duyệt."
            : $"Giáo viên {mentorName} vừa gửi đề xuất mở lớp \"{titleLabel}\" ({weeklySessionCount} buổi/tuần × {proposal.CourseDurationWeeks} tuần) chờ duyệt.";

        var notifications = adminIds.Select(adminId => new
        {
            UserId = adminId,
            Title = adminNotifTitle,
            Message = adminNotifMessage,
            Type = "MentorSchedule",
            LinkUrl = $"/admin/mentor-schedules/?proposalId={proposal.Id}",
        }).ToList();

        notifications.Add(new
        {
            UserId = mentorId,
            Title = isResubmission
                ? (string.IsNullOrWhiteSpace(levelCode)
                    ? "Đã gửi lại đề xuất mở lớp JLPT"
                    : $"Đã gửi lại đề xuất mở lớp JLPT {levelCode}")
                : "Đã gửi đề xuất mở lớp JLPT",
            Message = isResubmission
                ? $"Đề xuất mở lớp \"{titleLabel}\" đã được cập nhật và gửi lại xét duyệt."
                : $"Đề xuất mở lớp \"{titleLabel}\" ({weeklySessionCount} buổi/tuần × {proposal.CourseDurationWeeks} tuần) đã được gửi tới Quản trị viên và đang chờ duyệt.",
            Type = "MentorSchedule",
            LinkUrl = $"/mentor-shift-schedule/?proposalId={proposal.Id}",
        });

        unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
        {
            EventKey = $"proposal-submitted:{proposal.Id}:v{proposal.Revision}",
            AggregateType = "MentorTeachingProposal",
            AggregateId = proposal.Id.ToString(CultureInfo.InvariantCulture),
            EventType = "CohortLearningNotification",
            EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new { Notifications = notifications }),
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
        });
    }

    private async Task<ApiResponse<TeachingProposalResponse>> BuildSubmittedProposalSuccessResponseAsync(
        int proposalId, int mentorId, MentorGroupPolicy policy, DateTime nowUtc, CancellationToken ct)
    {
        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == proposalId, ct);
        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyAdminsAsync("proposal_submitted", ct);
            await sidebarNotifier.NotifyUsersAsync([mentorId], "proposal_submitted", ct);
        }
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(mentorId, ct);
        var dto = MapProposal(loaded, policy, qualCodes, null, includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(mentorId, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    private sealed class WeeklySlotEqualityComparer : IEqualityComparer<WeeklySessionSlotDto>
    {
        public static readonly WeeklySlotEqualityComparer Instance = new();
        public bool Equals(WeeklySessionSlotDto? x, WeeklySessionSlotDto? y)
            => x is not null && y is not null
                && x.Weekday == y.Weekday
                && x.StartMinute == y.StartMinute
                && x.EndMinute == y.EndMinute;
        public int GetHashCode(WeeklySessionSlotDto obj) => HashCode.Combine(obj.Weekday, obj.StartMinute, obj.EndMinute);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> WithdrawProposalAsync(
        int id, WithdrawTeachingProposalRequest? request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể rút đề xuất.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        ApiResponse<TeachingProposalResponse>? failure = null;
        MentorTeachingProposal? removedSnapshot = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
                if (proposal is null)
                {
                    // Idempotent retry: đã được hủy và xóa trước đó
                    return;
                }

                if (proposal.Status == MentorTeachingProposalStatus.Withdrawn)
                {
                    // Legacy withdrawn record cleanup: xóa vĩnh viễn dữ liệu đã rút
                    removedSnapshot = proposal;
                    unitOfWork.MentorTeachingProposals.Remove(proposal);
                    await unitOfWork.SaveChangesAsync(token);
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.PendingReview && proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Chỉ có thể hủy đề xuất đang ở trạng thái Chờ duyệt hoặc Cần bổ sung.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (request?.ExpectedRevision.HasValue == true && request.ExpectedRevision.Value != proposal.Revision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất vừa được cập nhật. Vui lòng tải lại trước khi hủy.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                proposal.Status = MentorTeachingProposalStatus.Withdrawn;
                proposal.Revision++;
                proposal.UpdatedAtUtc = nowUtc;
                removedSnapshot = proposal;

                // Xóa hẳn dữ liệu đề xuất khi giáo viên hủy/rút (cascade xóa Events, giải phóng toàn bộ khung giờ)
                unitOfWork.MentorTeachingProposals.Remove(proposal);
                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất vừa thay đổi đồng thời. Vui lòng tải lại trước khi hủy.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }

        if (failure is not null)
            return failure;

        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyAdminsAsync("proposal_withdrawn", ct);
            await sidebarNotifier.NotifyUsersAsync([claims.Id], "proposal_withdrawn", ct);
        }

        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var dto = removedSnapshot is not null
            ? MapProposal(removedSnapshot, policy, qualCodes, null, includeHistory: false)
            : new TeachingProposalResponse
            {
                Id = id,
                MentorId = claims.Id,
                Status = MentorTeachingProposalStatus.Withdrawn,
            };
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ReopenForRevisionAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể khôi phục phiên đăng ký.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var effectivePolicy = policy!;
        var createdId = 0;
        ApiResponse<TeachingProposalResponse>? failure = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

            var capacityErr = await CheckMentorNewRegistrationCapacityAsync(claims.Id, effectivePolicy, nowUtc, token);
            if (capacityErr.HasValue)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(capacityErr.Value.Message, HttpStatusCode.Conflict, capacityErr.Value.Code);
                return;
            }

            var source = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
            if (source is null)
            {
                failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất gốc.", "PROPOSAL_NOT_FOUND");
                return;
            }

            if (source.Status != MentorTeachingProposalStatus.Rejected && source.Status != MentorTeachingProposalStatus.Withdrawn)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    "Đề xuất đang chờ duyệt hoặc đã duyệt không thể khôi phục phiên đăng ký mới.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_IMMUTABLE_STATUS");
                return;
            }

            var existingDrafts = await unitOfWork.MentorTeachingProposals.Query()
                .Where(p => p.MentorId == claims.Id && p.Status == MentorTeachingProposalStatus.Draft)
                .OrderByDescending(p => p.UpdatedAtUtc)
                .ToListAsync(token);

            if (existingDrafts.Count > 0)
            {
                var activeDraft = existingDrafts[0];
                activeDraft.CertificateLevelId = source.CertificateLevelId;
                activeDraft.ProgramType = source.ProgramType;
                activeDraft.CoursePlanId = source.CoursePlanId;
                activeDraft.CourseTitle = source.CourseTitle;
                activeDraft.CourseObjective = source.CourseObjective;
                activeDraft.ExpectedStartDate = source.ExpectedStartDate;
                activeDraft.CourseDurationWeeks = effectivePolicy.CourseDurationWeeks;
                activeDraft.SessionDurationMinutes = effectivePolicy.SessionMinutes;
                activeDraft.WeeklySessionsJson = source.WeeklySessionsJson;
                activeDraft.OptionalNotes = source.OptionalNotes;
                activeDraft.OptionalAttachmentsJson = source.OptionalAttachmentsJson;
                activeDraft.GroupPolicyId = effectivePolicy.Id;
                activeDraft.PolicyVersionSnapshot = effectivePolicy.Version;
                activeDraft.Revision++;
                activeDraft.UpdatedAtUtc = nowUtc;
                activeDraft.LastSavedAtUtc = nowUtc;
                activeDraft.Events.Add(new MentorTeachingProposalEvent
                {
                    ActorUserId = claims.Id,
                    ActorRole = Role.Mentor.ToString(),
                    Action = "ClonedFromProposal",
                    FromStatus = source.Status,
                    ToStatus = MentorTeachingProposalStatus.Draft,
                    RevisionAfter = activeDraft.Revision,
                    Reason = $"Khôi phục phiên đăng ký mới từ đề xuất #{source.Id}",
                    CreatedAtUtc = nowUtc,
                });
                unitOfWork.MentorTeachingProposals.Update(activeDraft);

                for (var i = 1; i < existingDrafts.Count; i++)
                    unitOfWork.MentorTeachingProposals.Remove(existingDrafts[i]);

                await unitOfWork.SaveChangesAsync(token);
                createdId = activeDraft.Id;
                return;
            }

            var clone = new MentorTeachingProposal
            {
                MentorId = claims.Id,
                CertificateLevelId = source.CertificateLevelId,
                ProgramType = source.ProgramType,
                CoursePlanId = source.CoursePlanId,
                CourseTitle = source.CourseTitle,
                CourseObjective = source.CourseObjective,
                ExpectedStartDate = source.ExpectedStartDate,
                CourseDurationWeeks = effectivePolicy.CourseDurationWeeks,
                SessionDurationMinutes = effectivePolicy.SessionMinutes,
                WeeklySessionsJson = source.WeeklySessionsJson,
                OptionalNotes = source.OptionalNotes,
                OptionalAttachmentsJson = source.OptionalAttachmentsJson,
                Status = MentorTeachingProposalStatus.Draft,
                Revision = 1,
                GroupPolicyId = effectivePolicy.Id,
                PolicyVersionSnapshot = effectivePolicy.Version,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                LastSavedAtUtc = nowUtc,
            };

            clone.Events.Add(new MentorTeachingProposalEvent
            {
                ActorUserId = claims.Id,
                ActorRole = Role.Mentor.ToString(),
                Action = "ClonedFromProposal",
                FromStatus = source.Status,
                ToStatus = MentorTeachingProposalStatus.Draft,
                RevisionAfter = 1,
                Reason = $"Khôi phục phiên đăng ký mới từ đề xuất #{source.Id}",
                CreatedAtUtc = nowUtc,
            });

            await unitOfWork.MentorTeachingProposals.AddAsync(clone, token);
            await unitOfWork.SaveChangesAsync(token);
            createdId = clone.Id;
        }, ct);

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == createdId, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, effectivePolicy, qualCodes, null, includeHistory: true));
    }

    // ───────────────────────────── ADMIN ENDPOINTS ─────────────────────────────

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetAdminProposalsAsync(
        string? status = null, int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ quản trị viên mới có quyền xem hàng đợi đề xuất mở lớp.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<List<TeachingProposalResponse>>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        var query = QueryProposalsWithIncludes()
            .Where(p => p.Status != MentorTeachingProposalStatus.Draft
                && p.Status != MentorTeachingProposalStatus.Withdrawn);

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim();
            query = query.Where(p => p.Status == normalized);
        }
        if (mentorId.HasValue && mentorId.Value > 0)
            query = query.Where(p => p.MentorId == mentorId.Value);
        if (certificateLevelId.HasValue && certificateLevelId.Value > 0)
            query = query.Where(p => p.CertificateLevelId == certificateLevelId.Value);

        var rows = await query
            .OrderBy(p => p.Status == MentorTeachingProposalStatus.PendingReview ? 0 : 1)
            .ThenByDescending(p => p.SubmittedAtUtc ?? p.UpdatedAtUtc)
            .ToListAsync(ct);

        var mentorIds = rows.Select(r => r.MentorId).Distinct().ToList();
        var qualsByMentor = await LoadMentorsQualifiedLevelCodesAsync(mentorIds, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(rows.Select(r => r.Id).ToList(), ct);

        var result = new List<TeachingProposalResponse>(rows.Count);
        var workloadCache = new Dictionary<int, MentorWorkloadSummaryResponse>();

        foreach (var row in rows)
        {
            var dto = MapProposal(row, policy, qualsByMentor.GetValueOrDefault(row.MentorId) ?? [], linkedCohorts.GetValueOrDefault(row.Id), includeHistory: false);
            if (!workloadCache.TryGetValue(row.MentorId, out var workload))
            {
                workload = await ComputeMentorWorkloadAsync(row.MentorId, policy, nowUtc, ct);
                workloadCache[row.MentorId] = workload;
            }
            dto.MentorWorkload = workload;

            if (row.Status == MentorTeachingProposalStatus.PendingReview)
            {
                var check = await EvaluateProposalAsync(CandidateFromEntity(row), policy, nowUtc, ct, isApprovalOrPublication: true);
                dto.HasConflicts = !check.CanSubmit;
                dto.ValidationIssues = check.Issues;
            }

            result.Add(dto);
        }

        return ApiResponse<List<TeachingProposalResponse>>.Success(result);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> GetAdminProposalByIdAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ quản trị viên mới có quyền xem chi tiết đề xuất này.", HttpStatusCode.Forbidden);

        var proposal = await QueryProposalsWithIncludes(includeEvents: true)
            .FirstOrDefaultAsync(p => p.Id == id && p.Status != MentorTeachingProposalStatus.Withdrawn, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Thông tin đề xuất này không còn khả dụng hoặc đã được cập nhật.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(proposal.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, policy, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(proposal.MentorId, policy, nowUtc, ct);

        if (proposal.Status == MentorTeachingProposalStatus.PendingReview)
        {
            var check = await EvaluateProposalAsync(CandidateFromEntity(proposal), policy, nowUtc, ct, isApprovalOrPublication: true);
            dto.HasConflicts = !check.CanSubmit;
            dto.ValidationIssues = check.Issues;
            dto.OccurrencesPreview = check.OccurrencesPreview;
        }

        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ReviewProposalAsync(
        int id, ReviewTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ quản trị viên mới có quyền phê duyệt đề xuất mở lớp.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.Decision))
            return ApiResponse<TeachingProposalResponse>.Fail("Vui lòng chọn quyết định duyệt hợp lệ.");

        var decision = request.Decision.Trim();
        var isApprove = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isRequestChanges = string.Equals(decision, "RequestChanges", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isRequestChanges && !isReject)
            return ApiResponse<TeachingProposalResponse>.Fail("Quyết định xét duyệt không hợp lệ (chỉ hỗ trợ Phê duyệt, Yêu cầu bổ sung hoặc Từ chối).", HttpStatusCode.BadRequest, "INVALID_REVIEW_DECISION");

        var trimmedReason = TrimOrNull(request.Reason, 1500);
        if ((isRequestChanges || isReject) && string.IsNullOrWhiteSpace(trimmedReason))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng nhập lý do rõ ràng khi từ chối hoặc yêu cầu Giáo viên chỉnh sửa đề xuất.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_REVIEW_REASON_REQUIRED");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var adminUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == claims.Id)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        var adminName = adminUser is null
            ? $"Admin #{claims.Id}"
            : $"{adminUser.FirstName} {adminUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(adminName)) adminName = adminUser?.Email ?? $"Admin #{claims.Id}";

        ApiResponse<TeachingProposalResponse>? failure = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var lookup = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => new { p.Id, p.MentorId })
                    .FirstOrDefaultAsync(token);
                if (lookup is null)
                {
                    failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                // Khóa lịch Mentor trước khi load tracked entity để tránh read-before-lock race khi 2 Admin duyệt song song
                await unitOfWork.StudyCourses.LockScheduleAsync(lookup.MentorId, lookup.MentorId, token);

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id, token);
                if (proposal is null)
                {
                    failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.PendingReview)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất này không còn ở trạng thái Chờ duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_NOT_PENDING");
                    return;
                }

                if (proposal.Revision != request.ExpectedRevision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trước khi quyết định.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                var fromStatus = proposal.Status;
                var generatedSlotIds = new List<int>();
                StudyCohort? createdCohort = null;

                if (isApprove)
                {
                    var candidate = CandidateFromEntity(proposal);
                    var validation = await EvaluateProposalAsync(candidate, policy, nowUtc, token, isApprovalOrPublication: true);
                    if (!validation.CanSubmit)
                    {
                        var firstErr = validation.Issues.FirstOrDefault(i => i.Severity == "Error");
                        failure = ApiResponse<TeachingProposalResponse>.Fail(
                            firstErr?.Message ?? "Không thể phê duyệt vì lịch dạy không còn hợp lệ tại thời điểm duyệt.",
                            HttpStatusCode.Conflict,
                            firstErr?.Code ?? "PROPOSAL_APPROVAL_CONFLICT",
                            errors: validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message).ToList());
                        return;
                    }

                    var nextRevision = proposal.Revision + 1;
                    var createdSlots = new List<MentorAvailability>(validation.OccurrencesPreview.Count);
                    foreach (var occ in validation.OccurrencesPreview)
                    {
                        var localDate = DateOnly.ParseExact(occ.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var slot = new MentorAvailability
                        {
                            MentorId = proposal.MentorId,
                            TeachingProposalId = proposal.Id,
                            GenerationKey = $"proposal:{proposal.Id}:v{nextRevision}:s{occ.SessionNumber}",
                            PolicyVersion = policy.Version,
                            GroupPolicyId = policy.Id,
                            SessionMinutesSnapshot = proposal.SessionDurationMinutes,
                            RestMinutesSnapshot = policy.RestMinutes,
                            LocalBusinessDate = localDate,
                            MaxLearners = policy.MaxLearners,
                            BookedCount = 0,
                            StartTime = DateTime.SpecifyKind(occ.StartUtc, DateTimeKind.Unspecified),
                            EndTime = DateTime.SpecifyKind(occ.EndUtc, DateTimeKind.Unspecified),
                            RoomId = Guid.NewGuid(),
                            AttendanceTrackingEnabled = true,
                            IsApproved = true,
                            PublishedAt = nowUtc,
                            AdminName = adminName,
                            IsDeleted = false,
                            CreatedDate = nowUtc,
                            ModifiedDate = nowUtc,
                        };
                        createdSlots.Add(slot);
                        await unitOfWork.MentorAvailabilities.AddAsync(slot, token);
                    }

                    proposal.Status = MentorTeachingProposalStatus.Approved;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.GroupPolicyId = policy.Id;
                    proposal.PolicyVersionSnapshot = policy.Version;
                    proposal.Revision = nextRevision;
                    proposal.UpdatedAtUtc = nowUtc;

                    unitOfWork.MentorTeachingProposals.Update(proposal);
                    await unitOfWork.SaveChangesAsync(token);
                    generatedSlotIds = createdSlots.Select(s => s.Id).ToList();

                    // Tự động tạo lớp học và mở tuyển sinh ngay trong cùng giao dịch với snapshot học phí bất biến
                    createdCohort = await EnsureCohortForApprovedProposalAsync(proposal, createdSlots, policy, nowUtc, token);
                }
                else if (isRequestChanges)
                {
                    proposal.Status = MentorTeachingProposalStatus.ChangesRequested;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.SubmissionIdempotencyKey = null; // Cho phép gửi lại sau khi chỉnh sửa
                    proposal.Revision++;
                    proposal.UpdatedAtUtc = nowUtc;
                    unitOfWork.MentorTeachingProposals.Update(proposal);
                }
                else
                {
                    proposal.Status = MentorTeachingProposalStatus.Rejected;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.Revision++;
                    proposal.UpdatedAtUtc = nowUtc;
                    unitOfWork.MentorTeachingProposals.Update(proposal);
                }

                await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
                {
                    ProposalId = proposal.Id,
                    ActorUserId = claims.Id,
                    ActorRole = Role.Admin.ToString(),
                    Action = isApprove ? "Approved" : isRequestChanges ? "ChangesRequested" : "Rejected",
                    FromStatus = fromStatus,
                    ToStatus = proposal.Status,
                    RevisionAfter = proposal.Revision,
                    Reason = trimmedReason,
                    SnapshotJson = generatedSlotIds.Count > 0
                        ? JsonSerializer.Serialize(new
                        {
                            GeneratedAvailabilityIds = generatedSlotIds,
                            LinkedCohortId = createdCohort?.Id,
                            LinkedCohortName = createdCohort?.Name,
                            TotalTuitionSnapshot = createdCohort?.TotalTuitionSnapshot,
                        }, JsonOpts)
                        : null,
                    CreatedAtUtc = nowUtc,
                }, token);

                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = claims.Id,
                    EntityType = "MentorTeachingProposal",
                    EntityId = proposal.Id,
                    Action = isApprove ? "Approved" : isRequestChanges ? "ChangesRequested" : "Rejected",
                    BeforeJson = JsonSerializer.Serialize(new { Status = fromStatus, Revision = request.ExpectedRevision }),
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        proposal.Status,
                        proposal.Revision,
                        GeneratedSlotCount = generatedSlotIds.Count,
                        LinkedCohortId = createdCohort?.Id,
                        TotalTuitionSnapshot = createdCohort?.TotalTuitionSnapshot,
                    }),
                    Reason = trimmedReason,
                    CreatedAtUtc = nowUtc,
                }, token);

                var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất mở lớp" : proposal.CourseTitle;
                var levelCode = proposal.CertificateLevelId.HasValue
                    ? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                        .Where(l => l.Id == proposal.CertificateLevelId.Value)
                        .Select(l => l.Code)
                        .FirstOrDefaultAsync(token)
                    : null;
                var levelLabel = string.IsNullOrWhiteSpace(levelCode) ? "JLPT" : $"JLPT {levelCode}";
                var notifTitle = isApprove
                    ? $"Đề xuất mở lớp {levelLabel} đã được phê duyệt và mở tuyển sinh"
                    : isRequestChanges
                        ? $"Đề xuất mở lớp {levelLabel} cần bổ sung"
                        : $"Đề xuất mở lớp {levelLabel} chưa được chấp thuận";
                var notifMessage = isApprove
                    ? $"Lớp \"{createdCohort?.Name ?? titleLabel}\" ({generatedSlotIds.Count} buổi) đã được phê duyệt và mở tuyển sinh ngay. Bạn có thể theo dõi học viên ghi danh trong mục Quản lý lớp học."
                    : isRequestChanges
                        ? $"Quản trị viên yêu cầu bạn bổ sung thông tin cho đề xuất mở lớp \"{titleLabel}\": {trimmedReason}"
                        : $"Đề xuất mở lớp \"{titleLabel}\" chưa được chấp thuận. Xem chi tiết để biết lý do: {trimmedReason}";
                var notifLinkUrl = isApprove && createdCohort is not null
                    ? $"/mentor-study-courses/{createdCohort.Id}/"
                    : $"/mentor-shift-schedule/?proposalId={proposal.Id}";

                unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
                {
                    EventKey = $"proposal-reviewed:{proposal.Id}:v{proposal.Revision}",
                    AggregateType = "MentorTeachingProposal",
                    AggregateId = proposal.Id.ToString(CultureInfo.InvariantCulture),
                    EventType = "CohortLearningNotification",
                    EventVersion = 1,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        Notifications = new[]
                        {
                            new
                            {
                                UserId = proposal.MentorId,
                                Title = notifTitle,
                                Message = notifMessage,
                                Type = "MentorSchedule",
                                LinkUrl = notifLinkUrl,
                            },
                        },
                    }),
                    CreatedAtUtc = nowUtc,
                    AvailableAtUtc = nowUtc,
                });

                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã được xử lý bởi thao tác khác. Vui lòng tải lại danh sách.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Xung đột dữ liệu đồng thời khi phê duyệt đề xuất. Vui lòng tải lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyAdminsAsync("proposal_reviewed", ct);
            await sidebarNotifier.NotifyUsersAsync([loaded.MentorId], "proposal_reviewed", ct);
        }
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(loaded.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([loaded.Id], ct);
        var dto = MapProposal(loaded, policy, qualCodes, linkedCohorts.GetValueOrDefault(loaded.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(loaded.MentorId, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<BulkApproveTeachingProposalsResponse>> PrevalidateBulkApproveAsync(
        BulkApproveTeachingProposalsRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Chỉ quản trị viên mới có quyền kiểm tra phê duyệt hàng loạt đề xuất mở lớp.",
                HttpStatusCode.Forbidden);

        var (normalizedItems, inputError) = NormalizeBulkApproveItems(request);
        if (inputError is not null)
            return inputError;

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<BulkApproveTeachingProposalsResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var evaluation = await EvaluateBulkApproveBatchAsync(normalizedItems, policy, nowUtc, requireExpectedRevision: false, ct);
        return ApiResponse<BulkApproveTeachingProposalsResponse>.Success(evaluation.Response);
    }

    public async Task<ApiResponse<BulkApproveTeachingProposalsResponse>> BulkApproveProposalsAsync(
        BulkApproveTeachingProposalsRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Chỉ quản trị viên mới có quyền phê duyệt hàng loạt đề xuất mở lớp.",
                HttpStatusCode.Forbidden);

        var (normalizedItems, inputError) = NormalizeBulkApproveItems(request);
        if (inputError is not null)
            return inputError;

        var trimmedReason = TrimOrNull(request.Reason, 1500);
        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<BulkApproveTeachingProposalsResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var adminUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == claims.Id)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        var adminName = adminUser is null
            ? $"Admin #{claims.Id}"
            : $"{adminUser.FirstName} {adminUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(adminName)) adminName = adminUser?.Email ?? $"Admin #{claims.Id}";

        ApiResponse<BulkApproveTeachingProposalsResponse>? failure = null;
        BulkApproveTeachingProposalsResponse? committedResponse = null;
        var approvedMentorIds = new List<int>();
        var batchId = Guid.NewGuid().ToString("N")[..12];

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var requestedIds = normalizedItems.Select(x => x.ProposalId).ToList();

                // Khóa lịch tất cả Giáo viên liên quan theo thứ tự MentorId tăng dần để tránh deadlock khi 2 Admin duyệt song song
                var mentorIdsToLock = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .Where(p => requestedIds.Contains(p.Id))
                    .Select(p => p.MentorId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToListAsync(token);

                foreach (var mentorId in mentorIdsToLock)
                {
                    await unitOfWork.StudyCourses.LockScheduleAsync(mentorId, mentorId, token);
                }

                // Tái kiểm tra toàn bộ quy tắc từng đề xuất và xung đột chéo trong cùng batch sau khi giữ lock
                var evaluation = await EvaluateBulkApproveBatchAsync(
                    normalizedItems,
                    policy,
                    nowUtc,
                    requireExpectedRevision: true,
                    token);

                if (!evaluation.Response.AllEligible)
                {
                    var invalidItems = evaluation.Response.Results.Where(r => !r.IsEligible).ToList();
                    var firstIssue = invalidItems.SelectMany(r => r.Issues).FirstOrDefault(i => i.Severity == "Error");
                    var errorSummary = invalidItems
                        .SelectMany(r => r.Issues.Where(i => i.Severity == "Error").Take(2)
                            .Select(i => $"#{r.ProposalId} ({(string.IsNullOrWhiteSpace(r.MentorName) ? $"Đề xuất #{r.ProposalId}" : r.MentorName)}): {i.Message}"))
                        .Take(10)
                        .ToList();

                    failure = ApiResponse<BulkApproveTeachingProposalsResponse>.FailWithResult(
                        evaluation.Response,
                        $"Không thể phê duyệt hàng loạt vì có {invalidItems.Count}/{evaluation.Response.TotalRequested} đề xuất không đủ điều kiện hoặc xung đột lịch.",
                        HttpStatusCode.Conflict,
                        firstIssue?.Code ?? "PROPOSAL_BULK_VALIDATION_FAILED",
                        errorSummary);
                    return;
                }

                // Load tracked entities để thực thi atomic commit toàn bộ batch
                var trackedProposals = await unitOfWork.MentorTeachingProposals.Query()
                    .Where(p => requestedIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, token);

                var pendingRecords = new List<(MentorTeachingProposal Proposal, int OldRevision, List<MentorAvailability> CreatedSlots, BulkApproveProposalItemResult ItemResult)>(normalizedItems.Count);

                foreach (var item in normalizedItems)
                {
                    if (!trackedProposals.TryGetValue(item.ProposalId, out var proposal)
                        || proposal.Status != MentorTeachingProposalStatus.PendingReview
                        || (item.ExpectedRevision.HasValue && proposal.Revision != item.ExpectedRevision.Value)
                        || !evaluation.OccurrencesByProposalId.TryGetValue(proposal.Id, out var occurrences)
                        || occurrences.Count == 0)
                    {
                        failure = ApiResponse<BulkApproveTeachingProposalsResponse>.FailWithResult(
                            evaluation.Response,
                            "Dữ liệu đề xuất vừa thay đổi trong lúc xử lý. Vui lòng kiểm tra lại danh sách và thử lại.",
                            HttpStatusCode.Conflict,
                            "PROPOSAL_REVISION_CONFLICT");
                        return;
                    }

                    var oldRevision = proposal.Revision;
                    var nextRevision = oldRevision + 1;
                    var createdSlots = new List<MentorAvailability>(occurrences.Count);

                    foreach (var occ in occurrences)
                    {
                        var localDate = DateOnly.ParseExact(occ.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var slot = new MentorAvailability
                        {
                            MentorId = proposal.MentorId,
                            TeachingProposalId = proposal.Id,
                            GenerationKey = $"proposal:{proposal.Id}:v{nextRevision}:s{occ.SessionNumber}",
                            PolicyVersion = policy.Version,
                            GroupPolicyId = policy.Id,
                            SessionMinutesSnapshot = proposal.SessionDurationMinutes,
                            RestMinutesSnapshot = policy.RestMinutes,
                            LocalBusinessDate = localDate,
                            MaxLearners = policy.MaxLearners,
                            BookedCount = 0,
                            StartTime = DateTime.SpecifyKind(occ.StartUtc, DateTimeKind.Unspecified),
                            EndTime = DateTime.SpecifyKind(occ.EndUtc, DateTimeKind.Unspecified),
                            RoomId = Guid.NewGuid(),
                            AttendanceTrackingEnabled = true,
                            IsApproved = true,
                            PublishedAt = nowUtc,
                            AdminName = adminName,
                            IsDeleted = false,
                            CreatedDate = nowUtc,
                            ModifiedDate = nowUtc,
                        };
                        createdSlots.Add(slot);
                        await unitOfWork.MentorAvailabilities.AddAsync(slot, token);
                    }

                    proposal.Status = MentorTeachingProposalStatus.Approved;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.GroupPolicyId = policy.Id;
                    proposal.PolicyVersionSnapshot = policy.Version;
                    proposal.Revision = nextRevision;
                    proposal.UpdatedAtUtc = nowUtc;
                    unitOfWork.MentorTeachingProposals.Update(proposal);

                    var itemResult = evaluation.Response.Results.First(r => r.ProposalId == proposal.Id);
                    pendingRecords.Add((proposal, oldRevision, createdSlots, itemResult));
                }

                // Lưu đợt 1 trong cùng transaction để lấy ID các MentorAvailability vừa sinh
                await unitOfWork.SaveChangesAsync(token);

                foreach (var (proposal, oldRevision, createdSlots, itemResult) in pendingRecords)
                {
                    var generatedSlotIds = createdSlots.Select(s => s.Id).ToList();
                    var createdCohort = await EnsureCohortForApprovedProposalAsync(proposal, createdSlots, policy, nowUtc, token);

                    itemResult.Status = MentorTeachingProposalStatus.Approved;
                    itemResult.CurrentRevision = proposal.Revision;
                    itemResult.Approved = true;
                    itemResult.GeneratedAvailabilityIds = generatedSlotIds;
                    itemResult.LinkedCohortId = createdCohort.Id;
                    itemResult.LinkedCohortName = createdCohort.Name;
                    itemResult.TuitionPerSession = createdCohort.TuitionPerSessionSnapshot ?? itemResult.TuitionPerSession;
                    itemResult.TotalCourseTuition = createdCohort.TotalTuitionSnapshot ?? itemResult.TotalCourseTuition;
                    itemResult.TuitionPolicyVersion = createdCohort.TuitionPolicyVersionSnapshot ?? policy.Version;

                    await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
                    {
                        ProposalId = proposal.Id,
                        ActorUserId = claims.Id,
                        ActorRole = Role.Admin.ToString(),
                        Action = "Approved",
                        FromStatus = MentorTeachingProposalStatus.PendingReview,
                        ToStatus = MentorTeachingProposalStatus.Approved,
                        RevisionAfter = proposal.Revision,
                        Reason = trimmedReason,
                        SnapshotJson = JsonSerializer.Serialize(new
                        {
                            GeneratedAvailabilityIds = generatedSlotIds,
                            LinkedCohortId = createdCohort.Id,
                            LinkedCohortName = createdCohort.Name,
                            TotalTuitionSnapshot = createdCohort.TotalTuitionSnapshot,
                            BulkApproval = true,
                            BulkBatchId = batchId,
                            BulkBatchSize = pendingRecords.Count,
                        }, JsonOpts),
                        CreatedAtUtc = nowUtc,
                    }, token);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = claims.Id,
                        EntityType = "MentorTeachingProposal",
                        EntityId = proposal.Id,
                        Action = "Approved",
                        BeforeJson = JsonSerializer.Serialize(new { Status = MentorTeachingProposalStatus.PendingReview, Revision = oldRevision }),
                        AfterJson = JsonSerializer.Serialize(new
                        {
                            proposal.Status,
                            proposal.Revision,
                            GeneratedSlotCount = generatedSlotIds.Count,
                            LinkedCohortId = createdCohort.Id,
                            TotalTuitionSnapshot = createdCohort.TotalTuitionSnapshot,
                            BulkApproval = true,
                            BulkBatchId = batchId,
                            BulkBatchSize = pendingRecords.Count,
                        }),
                        Reason = trimmedReason,
                        CreatedAtUtc = nowUtc,
                    }, token);

                    var bulkLevelLabel = string.IsNullOrWhiteSpace(itemResult.JlptLevelCode)
                        ? "JLPT"
                        : $"JLPT {itemResult.JlptLevelCode}";
                    var titleLabel = string.IsNullOrWhiteSpace(createdCohort.Name)
                        ? (string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất mở lớp {bulkLevelLabel}" : proposal.CourseTitle)
                        : createdCohort.Name;
                    unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
                    {
                        EventKey = $"proposal-reviewed:{proposal.Id}:v{proposal.Revision}",
                        AggregateType = "MentorTeachingProposal",
                        AggregateId = proposal.Id.ToString(CultureInfo.InvariantCulture),
                        EventType = "CohortLearningNotification",
                        EventVersion = 1,
                        PayloadJson = JsonSerializer.Serialize(new
                        {
                            Notifications = new[]
                            {
                                new
                                {
                                    UserId = proposal.MentorId,
                                    Title = $"Đề xuất mở lớp {bulkLevelLabel} đã được phê duyệt và mở tuyển sinh",
                                    Message = $"Lớp \"{titleLabel}\" ({generatedSlotIds.Count} buổi) đã được phê duyệt và mở tuyển sinh ngay. Bạn có thể theo dõi học viên ghi danh trong mục Quản lý lớp học.",
                                    Type = "MentorSchedule",
                                    LinkUrl = $"/mentor-study-courses/{createdCohort.Id}/",
                                },
                            },
                        }),
                        CreatedAtUtc = nowUtc,
                        AvailableAtUtc = nowUtc,
                    });
                }

                await unitOfWork.SaveChangesAsync(token);

                approvedMentorIds = pendingRecords.Select(x => x.Proposal.MentorId).Distinct().ToList();
                evaluation.Response.Executed = true;
                evaluation.Response.ApprovedCount = pendingRecords.Count;
                evaluation.Response.ApprovedProposalIds = pendingRecords.Select(x => x.Proposal.Id).ToList();
                committedResponse = evaluation.Response;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Một hoặc nhiều đề xuất vừa được xử lý đồng thời bởi thao tác khác. Không có đề xuất nào được phê duyệt — vui lòng tải lại danh sách.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Xung đột dữ liệu đồng thời khi phê duyệt hàng loạt. Giao dịch đã được hoàn tác — vui lòng tải lại và thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        if (sidebarNotifier is not null && committedResponse is not null && committedResponse.ApprovedCount > 0)
        {
            await sidebarNotifier.NotifyAdminsAsync("proposal_reviewed", ct);
            if (approvedMentorIds.Count > 0)
                await sidebarNotifier.NotifyUsersAsync(approvedMentorIds, "proposal_reviewed", ct);
        }

        return ApiResponse<BulkApproveTeachingProposalsResponse>.Success(committedResponse!);
    }

    private static (List<BulkApproveProposalItemRequest> Items, ApiResponse<BulkApproveTeachingProposalsResponse>? Error) NormalizeBulkApproveItems(
        BulkApproveTeachingProposalsRequest? request)
    {
        if (request?.Items is null || request.Items.Count == 0)
        {
            return ([], ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Vui lòng chọn ít nhất một đề xuất để phê duyệt.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_BULK_EMPTY"));
        }

        if (request.Items.Count > 50)
        {
            return ([], ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Chỉ có thể phê duyệt tối đa 50 đề xuất trong một lần thao tác.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_BULK_LIMIT_EXCEEDED"));
        }

        var normalized = new List<BulkApproveProposalItemRequest>(request.Items.Count);
        var seen = new HashSet<int>();
        foreach (var item in request.Items)
        {
            if (item is null || item.ProposalId <= 0)
            {
                return ([], ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                    "Danh sách phê duyệt chứa mã đề xuất không hợp lệ.",
                    HttpStatusCode.BadRequest,
                    "PROPOSAL_ID_INVALID"));
            }

            if (seen.Add(item.ProposalId))
            {
                normalized.Add(item);
            }
        }

        return (normalized, null);
    }

    private sealed record BulkApproveEvaluationResult(
        BulkApproveTeachingProposalsResponse Response,
        Dictionary<int, List<GeneratedSessionOccurrenceResponse>> OccurrencesByProposalId);

    private async Task<BulkApproveEvaluationResult> EvaluateBulkApproveBatchAsync(
        List<BulkApproveProposalItemRequest> items,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        bool requireExpectedRevision,
        CancellationToken ct)
    {
        var requestedIds = items.Select(i => i.ProposalId).ToList();
        var loadedProposals = await QueryProposalsWithIncludes(includeEvents: false)
            .Where(p => requestedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var resultsById = new Dictionary<int, BulkApproveProposalItemResult>(items.Count);
        var orderedResults = new List<BulkApproveProposalItemResult>(items.Count);
        var occurrencesById = new Dictionary<int, List<GeneratedSessionOccurrenceResponse>>();
        var pendingEntities = new List<MentorTeachingProposal>();

        foreach (var reqItem in items)
        {
            if (!loadedProposals.TryGetValue(reqItem.ProposalId, out var proposal))
            {
                var missingRes = new BulkApproveProposalItemResult
                {
                    ProposalId = reqItem.ProposalId,
                    Status = "NotFound",
                    IsEligible = false,
                    Issues = [Error("PROPOSAL_NOT_FOUND", "Không tìm thấy đề xuất mở lớp (có thể đã bị xóa).")],
                };
                resultsById[reqItem.ProposalId] = missingRes;
                orderedResults.Add(missingRes);
                continue;
            }

            var mentorName = proposal.Mentor is null
                ? $"Mentor #{proposal.MentorId}"
                : $"{proposal.Mentor.FirstName} {proposal.Mentor.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(mentorName)) mentorName = proposal.Mentor?.Email ?? $"Mentor #{proposal.MentorId}";

            var weeklySessions = DeserializeSessions(proposal.WeeklySessionsJson);
            var durationWeeks = proposal.CourseDurationWeeks > 0 ? proposal.CourseDurationWeeks : policy.CourseDurationWeeks;
            var totalSessions = weeklySessions.Count * durationWeeks;
            var tuitionPerSession = policy.GetTuitionPerSession(proposal.CertificateLevel?.Code);
            var itemResult = new BulkApproveProposalItemResult
            {
                ProposalId = proposal.Id,
                MentorId = proposal.MentorId,
                MentorName = mentorName,
                JlptLevelCode = proposal.CertificateLevel?.Code,
                CourseTitle = proposal.CourseTitle,
                ExpectedStartDate = proposal.ExpectedStartDate,
                WeeklySessionCount = weeklySessions.Count,
                TotalSessions = totalSessions,
                TuitionPerSession = tuitionPerSession,
                TotalCourseTuition = tuitionPerSession * totalSessions,
                TuitionPolicyVersion = tuitionPerSession > 0 ? policy.Version : null,
                Status = proposal.Status,
                CurrentRevision = proposal.Revision,
            };

            if (proposal.Status != MentorTeachingProposalStatus.PendingReview)
            {
                var statusReason = proposal.Status switch
                {
                    MentorTeachingProposalStatus.Withdrawn => "Giáo viên đã rút lại đề xuất này.",
                    MentorTeachingProposalStatus.Approved => "Đề xuất này đã được phê duyệt trước đó.",
                    MentorTeachingProposalStatus.Rejected => "Đề xuất này đã bị từ chối.",
                    MentorTeachingProposalStatus.ChangesRequested => "Đề xuất này đang chờ giáo viên chỉnh sửa bổ sung.",
                    _ => "Đề xuất này không còn ở trạng thái Chờ duyệt.",
                };
                itemResult.Issues.Add(Error("PROPOSAL_NOT_PENDING", statusReason));
            }
            else
            {
                if (requireExpectedRevision && (!reqItem.ExpectedRevision.HasValue || proposal.Revision != reqItem.ExpectedRevision.Value))
                {
                    itemResult.Issues.Add(Error(
                        "PROPOSAL_REVISION_CONFLICT",
                        "Đề xuất vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trước khi phê duyệt."));
                }

                var validation = await EvaluateProposalAsync(
                    CandidateFromEntity(proposal),
                    policy,
                    nowUtc,
                    ct,
                    isApprovalOrPublication: true);

                foreach (var err in validation.Issues.Where(i => i.Severity == "Error"))
                {
                    itemResult.Issues.Add(err);
                }

                if (validation.OccurrencesPreview.Count > 0)
                {
                    itemResult.TotalSessions = validation.OccurrencesPreview.Count;
                    occurrencesById[proposal.Id] = validation.OccurrencesPreview;
                }

                pendingEntities.Add(proposal);
            }

            resultsById[proposal.Id] = itemResult;
            orderedResults.Add(itemResult);
        }

        // Kiểm tra giới hạn số lớp theo cấp độ/tổng lớp & xung đột chéo giữa chính các đề xuất trong cùng một batch theo từng Giáo viên
        var restMinutes = Math.Max(0, policy.RestMinutes);
        foreach (var mentorGroup in pendingEntities.GroupBy(p => p.MentorId))
        {
            var groupList = mentorGroup.ToList();
            if (groupList.Count < 2) continue;

            // (a) Giới hạn số lớp đồng thời theo từng cấp độ JLPT và tổng số lớp đang hoạt động theo chính sách hiệu lực
            var excludeSet = groupList.Select(p => p.Id).ToList();
            var baseline = await BuildLevelBlockingMapAsync(mentorGroup.Key, excludeSet, policy, nowUtc, ct);
            var runningByLevel = new Dictionary<int, int>(baseline.ActiveCountByLevel);
            var runningTotal = baseline.TotalActiveCount;
            var batchMaxTotal = Math.Max(1, policy.MaxTotalActiveClassesPerMentor);
            foreach (var candidateItem in groupList.OrderBy(p => p.SubmittedAtUtc ?? p.UpdatedAtUtc).ThenBy(p => p.Id))
            {
                if (!candidateItem.CertificateLevelId.HasValue || candidateItem.CertificateLevelId.Value <= 0)
                    continue;
                var lvlId = candidateItem.CertificateLevelId.Value;
                var lvlCode = candidateItem.CertificateLevel?.Code ?? "JLPT";
                var lvlLimit = policy.GetMaxActiveClassesForLevel(lvlId, lvlCode);
                var resItem = resultsById[candidateItem.Id];
                var alreadyBlockedLevel = resItem.Issues.Any(i => i.Code is "PROPOSAL_DUPLICATE_ACTIVE_LEVEL" or "PROPOSAL_LEVEL_DISABLED");
                var alreadyBlockedTotal = resItem.Issues.Any(i => i.Code == "MENTOR_TOTAL_CLASS_LIMIT_REACHED");
                var nextLevelCount = runningByLevel.GetValueOrDefault(lvlId) + 1;
                var nextTotalCount = runningTotal + 1;
                var blocked = alreadyBlockedLevel || alreadyBlockedTotal;

                if (!alreadyBlockedLevel && (lvlLimit <= 0 || nextLevelCount > lvlLimit))
                {
                    blocked = true;
                    resItem.Issues.Add(new ProposalValidationIssue
                    {
                        Code = "PROPOSAL_BULK_LEVEL_LIMIT_EXCEEDED",
                        Severity = "Error",
                        Message = lvlLimit <= 0
                            ? $"Cấp độ {lvlCode} hiện tạm ngưng nhận mở lớp mới theo cấu hình."
                            : $"Vượt giới hạn tối đa {lvlLimit} lớp/đề xuất đang hoạt động ở cấp độ {lvlCode} (tính cả các đề xuất đang chọn duyệt hàng loạt).",
                        ConflictJlptLevel = lvlCode,
                        ConflictEntityType = "TeachingProposal",
                        ConflictProposalId = candidateItem.Id,
                    });
                }

                if (!alreadyBlockedTotal && nextTotalCount > batchMaxTotal)
                {
                    blocked = true;
                    resItem.Issues.Add(new ProposalValidationIssue
                    {
                        Code = "PROPOSAL_BULK_TOTAL_LIMIT_EXCEEDED",
                        Severity = "Error",
                        Message = $"Vượt tổng giới hạn tối đa {batchMaxTotal} lớp/đề xuất đang hoạt động của Giáo viên khi duyệt hàng loạt.",
                        ConflictJlptLevel = lvlCode,
                        ConflictEntityType = "TeachingProposal",
                        ConflictProposalId = candidateItem.Id,
                    });
                }

                if (!blocked)
                {
                    runningByLevel[lvlId] = nextLevelCount;
                    runningTotal = nextTotalCount;
                }
            }

            // (b) Trùng khung giờ hoặc vi phạm khoảng nghỉ tối thiểu theo chính sách giữa 2 đề xuất trong batch
            for (var i = 0; i < groupList.Count; i++)
            {
                for (var j = i + 1; j < groupList.Count; j++)
                {
                    var a = groupList[i];
                    var b = groupList[j];
                    var resA = resultsById[a.Id];
                    var resB = resultsById[b.Id];
                    var titleA = string.IsNullOrWhiteSpace(a.CourseTitle) ? $"Đề xuất #{a.Id}" : a.CourseTitle;
                    var titleB = string.IsNullOrWhiteSpace(b.CourseTitle) ? $"Đề xuất #{b.Id}" : b.CourseTitle;

                    if (!occurrencesById.TryGetValue(a.Id, out var occsA) || !occurrencesById.TryGetValue(b.Id, out var occsB))
                        continue;

                    var reportedOverlap = false;
                    var reportedRest = false;
                    foreach (var oa in occsA)
                    {
                        if (reportedOverlap && reportedRest) break;
                        var aStart = MentorTeachingConflictService.Utc(oa.StartUtc);
                        var aEnd = MentorTeachingConflictService.Utc(oa.EndUtc);

                        foreach (var ob in occsB)
                        {
                            var bStart = MentorTeachingConflictService.Utc(ob.StartUtc);
                            var bEnd = MentorTeachingConflictService.Utc(ob.EndUtc);

                            if (!reportedOverlap && aStart < bEnd && bStart < aEnd)
                            {
                                reportedOverlap = true;
                                AddUniqueBatchIssue(resA, new ProposalValidationIssue
                                {
                                    Code = "PROPOSAL_BULK_SCHEDULE_CONFLICT",
                                    Severity = "Error",
                                    Message = $"Trùng lịch dạy ({oa.LocalDate} · {oa.LocalStart}–{oa.LocalEnd}) với đề xuất đang chọn cùng lúc ({titleB}).",
                                    ConflictSource = titleB,
                                    ConflictDate = ob.LocalDate,
                                    ConflictLocalStart = ob.LocalStart,
                                    ConflictLocalEnd = ob.LocalEnd,
                                    ConflictEntityType = "TeachingProposal",
                                    ConflictProposalId = b.Id,
                                }, skipIfExistingCodeWithSameProposal: MentorTeachingConflictService.ScheduleConflict);

                                AddUniqueBatchIssue(resB, new ProposalValidationIssue
                                {
                                    Code = "PROPOSAL_BULK_SCHEDULE_CONFLICT",
                                    Severity = "Error",
                                    Message = $"Trùng lịch dạy ({ob.LocalDate} · {ob.LocalStart}–{ob.LocalEnd}) với đề xuất đang chọn cùng lúc ({titleA}).",
                                    ConflictSource = titleA,
                                    ConflictDate = oa.LocalDate,
                                    ConflictLocalStart = oa.LocalStart,
                                    ConflictLocalEnd = oa.LocalEnd,
                                    ConflictEntityType = "TeachingProposal",
                                    ConflictProposalId = a.Id,
                                }, skipIfExistingCodeWithSameProposal: MentorTeachingConflictService.ScheduleConflict);
                            }
                            else if (!reportedRest && !(aStart < bEnd && bStart < aEnd))
                            {
                                var gap = bStart >= aEnd ? bStart - aEnd : aStart - bEnd;
                                if (gap < TimeSpan.FromMinutes(restMinutes))
                                {
                                    reportedRest = true;
                                    AddUniqueBatchIssue(resA, new ProposalValidationIssue
                                    {
                                        Code = "PROPOSAL_BULK_REST_VIOLATION",
                                        Severity = "Error",
                                        Message = $"Không đủ khoảng nghỉ tối thiểu {restMinutes} phút ({oa.LocalDate} · {oa.LocalStart}–{oa.LocalEnd}) với đề xuất đang chọn cùng lúc ({titleB}).",
                                        ConflictSource = titleB,
                                        ConflictDate = ob.LocalDate,
                                        ConflictLocalStart = ob.LocalStart,
                                        ConflictLocalEnd = ob.LocalEnd,
                                        ConflictEntityType = "TeachingProposal",
                                        ConflictProposalId = b.Id,
                                    }, skipIfExistingCodeWithSameProposal: MentorTeachingConflictService.RestViolation);

                                    AddUniqueBatchIssue(resB, new ProposalValidationIssue
                                    {
                                        Code = "PROPOSAL_BULK_REST_VIOLATION",
                                        Severity = "Error",
                                        Message = $"Không đủ khoảng nghỉ tối thiểu {restMinutes} phút ({ob.LocalDate} · {ob.LocalStart}–{ob.LocalEnd}) với đề xuất đang chọn cùng lúc ({titleA}).",
                                        ConflictSource = titleA,
                                        ConflictDate = oa.LocalDate,
                                        ConflictLocalStart = oa.LocalStart,
                                        ConflictLocalEnd = oa.LocalEnd,
                                        ConflictEntityType = "TeachingProposal",
                                        ConflictProposalId = a.Id,
                                    }, skipIfExistingCodeWithSameProposal: MentorTeachingConflictService.RestViolation);
                                }
                            }
                        }
                    }
                }
            }

            // (c) Tổng giới hạn buổi/ngày và buổi/tuần cộng dồn trong cùng batch + lịch đã cam kết hiện có trong DB
            var combined = groupList
                .Where(p => occurrencesById.ContainsKey(p.Id))
                .SelectMany(p => occurrencesById[p.Id].Select(o => new
                {
                    ProposalId = p.Id,
                    LocalDate = DateOnly.ParseExact(o.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                }))
                .ToList();

            var batchProposalIds = groupList.Select(p => p.Id).ToHashSet();
            var dbDayCounts = new Dictionary<DateOnly, int>();
            var dbWeekCounts = new Dictionary<DateOnly, int>();
            if (combined.Count > 0 && (policy.DailySessionCap > 0 || policy.WeeklySessionCap > 0))
            {
                var minLocalDate = combined.Min(x => x.LocalDate);
                var maxLocalDate = combined.Max(x => x.LocalDate);
                var windowStartUtc = LocalDateMinuteToUtc(LocalWeekStart(minLocalDate), 0);
                var windowEndUtc = LocalDateMinuteToUtc(LocalWeekStart(maxLocalDate).AddDays(7), 0);

                var committedStarts = new List<DateTime>();
                var cohortStarts = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                    .Where(g => g.Status != "Cancelled")
                    .SelectMany(g => g.Sessions
                        .Where(s => s.Availability.MentorId == mentorGroup.Key
                            && !s.Availability.IsDeleted
                            && s.Availability.RejectedAt == null
                            && s.Availability.StartTime < windowEndUtc
                            && s.Availability.EndTime > windowStartUtc)
                        .Select(s => s.Availability.StartTime))
                    .Distinct()
                    .ToListAsync(ct);
                committedStarts.AddRange(cohortStarts);

                var otherActiveProps = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .Where(p => p.MentorId == mentorGroup.Key
                        && !batchProposalIds.Contains(p.Id)
                        && p.ExpectedStartDate.HasValue
                        && (p.Status == MentorTeachingProposalStatus.PendingReview
                            || p.Status == MentorTeachingProposalStatus.ChangesRequested))
                    .ToListAsync(ct);
                foreach (var op in otherActiveProps)
                {
                    var opMinutes = op.SessionDurationMinutes > 0 ? op.SessionDurationMinutes : policy.SessionMinutes;
                    var opWeeks = op.CourseDurationWeeks > 0 ? op.CourseDurationWeeks : policy.CourseDurationWeeks;
                    var opSessions = DeserializeSessions(op.WeeklySessionsJson, opMinutes);
                    foreach (var occ in ExpandOccurrences(op.ExpectedStartDate!.Value, opSessions, opWeeks, opMinutes, DateTime.MinValue))
                    {
                        if (occ.StartUtc < windowEndUtc && occ.EndUtc > windowStartUtc)
                            committedStarts.Add(occ.StartUtc);
                    }
                }

                var bookingStarts = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
                    .Where(a => a.MentorId == mentorGroup.Key
                        && !a.IsDeleted
                        && a.IsApproved
                        && a.RejectedAt == null
                        && a.TeachingProposalId == null
                        && a.BookedCount > 0
                        && a.StartTime < windowEndUtc
                        && a.EndTime > windowStartUtc
                        && !unitOfWork.StudyCohorts.Query().Any(g => g.Sessions.Any(s => s.AvailabilityId == a.Id)))
                    .Select(a => a.StartTime)
                    .ToListAsync(ct);
                committedStarts.AddRange(bookingStarts);

                foreach (var utc in committedStarts)
                {
                    var localDate = DateOnly.FromDateTime(LocalTime(utc));
                    dbDayCounts[localDate] = dbDayCounts.GetValueOrDefault(localDate) + 1;
                    var weekStart = LocalWeekStart(localDate);
                    dbWeekCounts[weekStart] = dbWeekCounts.GetValueOrDefault(weekStart) + 1;
                }
            }

            if (policy.DailySessionCap > 0)
            {
                foreach (var dayGroup in combined.GroupBy(x => x.LocalDate))
                {
                    var count = dbDayCounts.GetValueOrDefault(dayGroup.Key) + dayGroup.Count();
                    if (count <= policy.DailySessionCap) continue;
                    foreach (var pid in dayGroup.Select(x => x.ProposalId).Distinct())
                    {
                        var res = resultsById[pid];
                        if (!res.Issues.Any(i => i.Code is MentorTeachingConflictService.DailyLimit or "PROPOSAL_BULK_DAILY_CAP_EXCEEDED"))
                        {
                            res.Issues.Add(Error(
                                "PROPOSAL_BULK_DAILY_CAP_EXCEEDED",
                                $"Tổng lịch dạy vào {dayGroup.Key:dd/MM/yyyy} sẽ lên tới {count} buổi, vượt giới hạn {policy.DailySessionCap} buổi/ngày."));
                        }
                    }
                }
            }

            if (policy.WeeklySessionCap > 0)
            {
                foreach (var weekGroup in combined.GroupBy(x => LocalWeekStart(x.LocalDate)))
                {
                    var count = dbWeekCounts.GetValueOrDefault(weekGroup.Key) + weekGroup.Count();
                    if (count <= policy.WeeklySessionCap) continue;
                    foreach (var pid in weekGroup.Select(x => x.ProposalId).Distinct())
                    {
                        var res = resultsById[pid];
                        if (!res.Issues.Any(i => i.Code is MentorTeachingConflictService.WeeklyLimit or "PROPOSAL_BULK_WEEKLY_CAP_EXCEEDED"))
                        {
                            res.Issues.Add(Error(
                                "PROPOSAL_BULK_WEEKLY_CAP_EXCEEDED",
                                $"Tổng lịch dạy trong tuần từ {weekGroup.Key:dd/MM/yyyy} sẽ lên tới {count} buổi, vượt giới hạn {policy.WeeklySessionCap} buổi/tuần."));
                        }
                    }
                }
            }
        }

        foreach (var r in orderedResults)
        {
            r.IsEligible = !r.Issues.Any(i => i.Severity == "Error");
        }

        var eligibleCount = orderedResults.Count(r => r.IsEligible);
        var invalidCount = orderedResults.Count - eligibleCount;

        return new BulkApproveEvaluationResult(
            new BulkApproveTeachingProposalsResponse
            {
                AllEligible = orderedResults.Count > 0 && invalidCount == 0,
                Executed = false,
                TotalRequested = orderedResults.Count,
                EligibleCount = eligibleCount,
                InvalidCount = invalidCount,
                ApprovedCount = 0,
                ApprovedProposalIds = [],
                Results = orderedResults,
            },
            occurrencesById);
    }

    private static void AddUniqueBatchIssue(
        BulkApproveProposalItemResult target,
        ProposalValidationIssue issue,
        string? skipIfExistingCodeWithSameProposal = null)
    {
        if (target.Issues.Any(existing =>
            (existing.Code == issue.Code || (skipIfExistingCodeWithSameProposal != null && existing.Code == skipIfExistingCodeWithSameProposal))
            && existing.ConflictProposalId == issue.ConflictProposalId))
        {
            return;
        }
        target.Issues.Add(issue);
    }

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetApprovedProposalsForCohortAsync(
        int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ quản trị viên mới có quyền xem đề xuất đã duyệt.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<List<TeachingProposalResponse>>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        var query = QueryProposalsWithIncludes()
            .Where(p => p.Status == MentorTeachingProposalStatus.Approved);

        if (mentorId.HasValue && mentorId.Value > 0)
            query = query.Where(p => p.MentorId == mentorId.Value);
        if (certificateLevelId.HasValue && certificateLevelId.Value > 0)
            query = query.Where(p => p.CertificateLevelId == certificateLevelId.Value);

        var rows = await query.OrderByDescending(p => p.ReviewedAtUtc ?? p.UpdatedAtUtc).ToListAsync(ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(rows.Select(r => r.Id).ToList(), ct);
        var qualsByMentor = await LoadMentorsQualifiedLevelCodesAsync(rows.Select(r => r.MentorId).Distinct().ToList(), ct);

        // Chỉ trả về các đề xuất đã duyệt còn buổi khả dụng trong tương lai và chưa gắn vào lớp nhóm đang hoạt động
        var eligible = rows
            .Where(p => !linkedCohorts.TryGetValue(p.Id, out var c) || c.Status == "Cancelled")
            .Select(p => MapProposal(p, policy, qualsByMentor.GetValueOrDefault(p.MentorId) ?? [], linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false))
            .Where(dto => dto.GeneratedAvailabilityIds.Count > 0)
            .ToList();

        return ApiResponse<List<TeachingProposalResponse>>.Success(eligible);
    }

    public async Task<ApiResponse<RepairApprovedCohortsResponse>> RepairApprovedCohortsAsync(CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<RepairApprovedCohortsResponse>.Fail(
                "Chỉ quản trị viên mới có quyền đồng bộ lớp học từ các đề xuất đã phê duyệt.",
                HttpStatusCode.Forbidden);

        var response = await BackfillApprovedProposalCohortsCoreAsync(claims.Id, ct);
        return ApiResponse<RepairApprovedCohortsResponse>.Success(response);
    }

    public Task<RepairApprovedCohortsResponse> BackfillApprovedProposalCohortsSystemAsync(CancellationToken ct = default)
        => BackfillApprovedProposalCohortsCoreAsync(actorUserId: null, ct);

    private async Task<RepairApprovedCohortsResponse> BackfillApprovedProposalCohortsCoreAsync(
        int? actorUserId,
        CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        if (policy is null)
        {
            return new RepairApprovedCohortsResponse
            {
                Notes = { "Hệ thống chưa có chính sách lớp học nhóm đang có hiệu lực." },
            };
        }

        var approvedProposals = await unitOfWork.MentorTeachingProposals.Query()
            .Include(p => p.CertificateLevel)
            .Include(p => p.GeneratedAvailabilities)
            .Where(p => p.Status == MentorTeachingProposalStatus.Approved)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var linkedMap = await LoadLinkedCohortsMapAsync(approvedProposals.Select(p => p.Id).ToList(), ct);
        var response = new RepairApprovedCohortsResponse
        {
            ScannedCount = approvedProposals.Count,
        };

        foreach (var proposal in approvedProposals)
        {
            if (linkedMap.TryGetValue(proposal.Id, out var existing) && existing.Status != "Cancelled")
            {
                response.SkippedCount++;
                continue;
            }

            var orderedSlots = proposal.GeneratedAvailabilities
                .Where(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null)
                .OrderBy(a => a.StartTime)
                .ToList();

            if (orderedSlots.Count == 0 || MentorTeachingConflictService.Utc(orderedSlots[0].StartTime) <= nowUtc)
            {
                response.SkippedCount++;
                response.Notes.Add($"Bỏ qua đề xuất \"{proposal.CourseTitle ?? "JLPT"}\" vì chưa có buổi học hợp lệ trong tương lai.");
                continue;
            }

            var levelCode = proposal.CertificateLevel?.Code;
            if (!proposal.CertificateLevelId.HasValue || string.IsNullOrWhiteSpace(levelCode) || policy.GetTuitionPerSession(levelCode) <= 0)
            {
                response.SkippedCount++;
                response.Notes.Add($"Bỏ qua đề xuất \"{proposal.CourseTitle ?? "JLPT"}\" vì trình độ hoặc đơn giá học phí chưa được cấu hình.");
                continue;
            }

            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(proposal.MentorId, proposal.MentorId, token);
                var tracked = await unitOfWork.MentorTeachingProposals.Query()
                    .Include(p => p.CertificateLevel)
                    .FirstAsync(p => p.Id == proposal.Id, token);
                var trackedSlots = await unitOfWork.MentorAvailabilities.Query()
                    .Where(a => a.TeachingProposalId == proposal.Id && !a.IsDeleted && a.IsApproved && a.RejectedAt == null)
                    .OrderBy(a => a.StartTime)
                    .ToListAsync(token);

                if (trackedSlots.Count == 0 || MentorTeachingConflictService.Utc(trackedSlots[0].StartTime) <= nowUtc)
                {
                    response.SkippedCount++;
                    return;
                }

                var alreadyLinked = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                    .AnyAsync(c => c.SourceProposalId == proposal.Id && c.Status != "Cancelled", token);
                if (alreadyLinked)
                {
                    response.SkippedCount++;
                    return;
                }

                var cohort = await EnsureCohortForApprovedProposalAsync(tracked, trackedSlots, policy, nowUtc, token);
                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = actorUserId,
                    EntityType = "StudyCohort",
                    EntityId = cohort.Id,
                    Action = actorUserId.HasValue ? "BackfilledFromApprovedProposal" : "AutoBackfilledFromApprovedProposal",
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        cohort.Id,
                        cohort.Name,
                        SourceProposalId = tracked.Id,
                        cohort.TotalTuitionSnapshot,
                    }),
                    CreatedAtUtc = nowUtc,
                }, token);
                await unitOfWork.SaveChangesAsync(token);
                response.CreatedCount++;
                response.CreatedCohortIds.Add(cohort.Id);
            }, ct);
        }

        if (sidebarNotifier is not null && response.CreatedCount > 0)
        {
            await sidebarNotifier.NotifyAdminsAsync("cohort_backfilled", ct);
        }

        return response;
    }

    public async Task<ApiResponse<RepairInvalidProposalSchedulesResponse>> RepairInvalidNeedsRevisionSchedulesAsync(CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<RepairInvalidProposalSchedulesResponse>.Fail(
                "Chỉ quản trị viên mới có quyền kiểm tra và khôi phục lịch đề xuất từ lịch sử sự kiện.",
                HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<RepairInvalidProposalSchedulesResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var allowedCounts = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv);
        var minPerWeek = allowedCounts.Min();
        var maxPerWeek = allowedCounts.Max();

        var candidates = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Include(p => p.Events)
            .Where(p => p.Status == MentorTeachingProposalStatus.ChangesRequested)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var response = new RepairInvalidProposalSchedulesResponse
        {
            ScannedCount = candidates.Count,
        };

        foreach (var candidate in candidates)
        {
            var currentSessions = DeserializeSessions(candidate.WeeklySessionsJson);
            if (currentSessions.Count >= minPerWeek && currentSessions.Count <= maxPerWeek)
                continue;

            response.InvalidFoundCount++;

            var snapshotMatch = FindLatestValidSnapshot(candidate.Events, policy, minPerWeek, maxPerWeek);
            if (snapshotMatch is null)
            {
                response.SkippedCount++;
                response.Items.Add(new RepairedProposalScheduleItem
                {
                    ProposalId = candidate.Id,
                    CourseTitle = candidate.CourseTitle,
                    JlptLevelCode = candidate.CertificateLevel?.Code,
                    Action = "Skipped",
                    PreviousWeeklyCount = currentSessions.Count,
                    RestoredWeeklyCount = currentSessions.Count,
                    Reason = $"Không tìm thấy ảnh chụp lịch hợp lệ ({minPerWeek}–{maxPerWeek} buổi/tuần) trong lịch sử sự kiện.",
                });
                continue;
            }

            var targetStartDate = ResolveRestoredStartDate(candidate.ExpectedStartDate, snapshotMatch.ExpectedStartDate, snapshotMatch.Sessions);
            var itemOutcome = new RepairedProposalScheduleItem
            {
                ProposalId = candidate.Id,
                CourseTitle = candidate.CourseTitle,
                JlptLevelCode = candidate.CertificateLevel?.Code,
                PreviousWeeklyCount = currentSessions.Count,
                RestoredWeeklyCount = snapshotMatch.Sessions.Count,
            };

            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(candidate.MentorId, candidate.MentorId, token);
                var tracked = await unitOfWork.MentorTeachingProposals.Query()
                    .Include(p => p.CertificateLevel)
                    .FirstOrDefaultAsync(p => p.Id == candidate.Id && p.Status == MentorTeachingProposalStatus.ChangesRequested, token);

                if (tracked is null)
                {
                    itemOutcome.Action = "Skipped";
                    itemOutcome.Reason = "Trạng thái đề xuất đã thay đổi trước khi khôi phục.";
                    return;
                }

                var recheck = new ProposalCandidate(
                    ProposalId: tracked.Id,
                    MentorId: tracked.MentorId,
                    CertificateLevelId: tracked.CertificateLevelId,
                    ProgramType: NormalizeProgramType(tracked.ProgramType),
                    CoursePlanId: tracked.CoursePlanId,
                    CourseObjective: string.IsNullOrWhiteSpace(tracked.CourseObjective) ? "Audit restore check" : tracked.CourseObjective,
                    ExpectedStartDate: targetStartDate,
                    CourseDurationWeeks: policy.CourseDurationWeeks,
                    SessionDurationMinutes: policy.SessionMinutes,
                    WeeklySessions: snapshotMatch.Sessions,
                    OptionalAttachments: DeserializeAttachments(tracked.OptionalAttachmentsJson));

                var validation = await EvaluateProposalAsync(recheck, policy, nowUtc, token, isApprovalOrPublication: true);
                var blockingErrors = validation.Issues.Where(i => i.Severity == "Error").ToList();
                if (blockingErrors.Count > 0 || !targetStartDate.HasValue)
                {
                    itemOutcome.Action = "Skipped";
                    itemOutcome.Reason = blockingErrors.FirstOrDefault()?.Message ?? "Lịch khôi phục bị xung đột hoặc không còn hợp lệ.";
                    return;
                }

                var restoredJson = JsonSerializer.Serialize(snapshotMatch.Sessions, JsonOpts);
                tracked.WeeklySessionsJson = restoredJson;
                tracked.ExpectedStartDate = targetStartDate;
                tracked.CourseDurationWeeks = policy.CourseDurationWeeks;
                tracked.SessionDurationMinutes = policy.SessionMinutes;
                tracked.Revision++;
                tracked.UpdatedAtUtc = nowUtc;
                tracked.LastSavedAtUtc = nowUtc;

                unitOfWork.MentorTeachingProposals.Update(tracked);
                await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
                {
                    ProposalId = tracked.Id,
                    ActorUserId = claims.Id,
                    ActorRole = Role.Admin.ToString(),
                    Action = "ScheduleRestoredFromAudit",
                    FromStatus = MentorTeachingProposalStatus.ChangesRequested,
                    ToStatus = MentorTeachingProposalStatus.ChangesRequested,
                    RevisionAfter = tracked.Revision,
                    Reason = $"Khôi phục lịch hợp lệ {snapshotMatch.Sessions.Count} buổi/tuần từ ảnh chụp lịch sử (Event #{snapshotMatch.EventId}).",
                    SnapshotJson = BuildSnapshotJson(tracked),
                    CreatedAtUtc = nowUtc,
                }, token);

                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = claims.Id,
                    EntityType = "MentorTeachingProposal",
                    EntityId = tracked.Id,
                    Action = "ScheduleRestoredFromAudit",
                    BeforeJson = candidate.WeeklySessionsJson,
                    AfterJson = restoredJson,
                    CreatedAtUtc = nowUtc,
                }, token);

                await unitOfWork.SaveChangesAsync(token);
                itemOutcome.Action = "Restored";
                itemOutcome.Reason = $"Đã khôi phục {snapshotMatch.Sessions.Count} buổi/tuần từ lịch sử sự kiện (Event #{snapshotMatch.EventId}).";
            }, ct);

            if (itemOutcome.Action == "Restored") response.RestoredCount++;
            else response.SkippedCount++;

            response.Items.Add(itemOutcome);
        }

        return ApiResponse<RepairInvalidProposalSchedulesResponse>.Success(response);
    }

    private sealed record HistoricalScheduleSnapshot(
        long EventId,
        DateOnly? ExpectedStartDate,
        List<WeeklySessionSlotDto> Sessions);

    private static HistoricalScheduleSnapshot? FindLatestValidSnapshot(
        IEnumerable<MentorTeachingProposalEvent> events,
        MentorGroupPolicy policy,
        int minPerWeek,
        int maxPerWeek)
    {
        foreach (var ev in events.OrderByDescending(e => e.Id))
        {
            if (string.IsNullOrWhiteSpace(ev.SnapshotJson)) continue;
            try
            {
                using var doc = JsonDocument.Parse(ev.SnapshotJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                JsonElement weeklyEl = default;
                var hasWeekly = false;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "weeklySessions", StringComparison.OrdinalIgnoreCase))
                    {
                        weeklyEl = prop.Value;
                        hasWeekly = prop.Value.ValueKind == JsonValueKind.Array;
                        break;
                    }
                }
                if (!hasWeekly) continue;

                var parsedSessions = DeserializeSessions(weeklyEl.GetRawText());
                if (parsedSessions.Count < minPerWeek || parsedSessions.Count > maxPerWeek) continue;

                var structureCheck = ValidateDraftFields(
                    new SaveTeachingProposalDraftRequest { WeeklySessions = parsedSessions },
                    policy,
                    out _,
                    out var normalized,
                    out _);
                if (structureCheck.HasValue || normalized.Count < minPerWeek || normalized.Count > maxPerWeek)
                    continue;

                DateOnly? snapStartDate = null;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "expectedStartDate", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && DateOnly.TryParseExact(prop.Value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        snapStartDate = d;
                        break;
                    }
                }

                return new HistoricalScheduleSnapshot(ev.Id, snapStartDate, normalized);
            }
            catch (JsonException)
            {
                // skip malformed historical snapshot
            }
        }
        return null;
    }

    private static DateOnly? ResolveRestoredStartDate(
        DateOnly? currentStartDate,
        DateOnly? snapshotStartDate,
        List<WeeklySessionSlotDto> restoredSessions)
    {
        var weekdays = restoredSessions.Select(s => s.Weekday).ToHashSet();
        if (currentStartDate.HasValue && weekdays.Contains((int)currentStartDate.Value.DayOfWeek))
            return currentStartDate;
        if (snapshotStartDate.HasValue && weekdays.Contains((int)snapshotStartDate.Value.DayOfWeek))
            return snapshotStartDate;
        return currentStartDate ?? snapshotStartDate;
    }

    private async Task<StudyCohort> EnsureCohortForApprovedProposalAsync(
        MentorTeachingProposal proposal,
        List<MentorAvailability> createdSlots,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        CancellationToken token)
    {
        var existingCohort = await unitOfWork.StudyCohorts.Query()
            .FirstOrDefaultAsync(c => c.SourceProposalId == proposal.Id && c.Status != "Cancelled", token);
        if (existingCohort is not null)
            return existingCohort;

        var orderedSlots = createdSlots.OrderBy(s => s.StartTime).ToList();
        if (orderedSlots.Count == 0)
            throw new InvalidOperationException("Không thể tạo lớp học khi chưa có buổi học được duyệt.");

        var levelId = proposal.CertificateLevelId
            ?? throw new InvalidOperationException("Đề xuất mở lớp thiếu trình độ JLPT.");
        var levelCode = proposal.CertificateLevel?.Code
            ?? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Where(l => l.Id == levelId)
                .Select(l => l.Code)
                .FirstOrDefaultAsync(token)
            ?? "JLPT";

        var weeklySessions = Math.Max(1, DeserializeSessions(proposal.WeeklySessionsJson).Count);
        var durationWeeks = proposal.CourseDurationWeeks > 0 ? proposal.CourseDurationWeeks : policy.CourseDurationWeeks;
        var sessionMinutes = proposal.SessionDurationMinutes > 0 ? proposal.SessionDurationMinutes : policy.SessionMinutes;
        var totalSessions = orderedSlots.Count;
        var tuitionPerSession = policy.GetTuitionPerSession(levelCode);
        var totalTuition = tuitionPerSession * totalSessions;

        StudyCoursePlan? plan = null;
        if (proposal.CoursePlanId.HasValue && proposal.CoursePlanId.Value > 0)
        {
            var candidatePlan = await unitOfWork.StudyCoursePlans.Query()
                .FirstOrDefaultAsync(p => p.Id == proposal.CoursePlanId.Value, token);
            if (candidatePlan is not null
                && string.Equals(candidatePlan.SessionType, "Group", StringComparison.OrdinalIgnoreCase)
                && candidatePlan.CertificateLevelId == levelId
                && candidatePlan.SessionsPerWeek == weeklySessions
                && (candidatePlan.TotalSessions ?? totalSessions) == totalSessions
                && (candidatePlan.SessionMinutes ?? sessionMinutes) == sessionMinutes
                && candidatePlan.Price == totalTuition
                && candidatePlan.IsPublished)
            {
                plan = candidatePlan;
            }
        }

        plan ??= await unitOfWork.StudyCoursePlans.Query()
            .Where(p => p.SessionType == "Group"
                && p.CertificateLevelId == levelId
                && p.SessionsPerWeek == weeklySessions
                && p.TotalSessions == totalSessions
                && p.SessionMinutes == sessionMinutes
                && p.Price == totalTuition
                && p.IsPublished)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(token);

        if (plan is null)
        {
            // Tạo gói lớp nhóm riêng theo đúng bảng giá thời điểm mở lớp, tuyệt đối không ghi đè gói của lớp cũ
            plan = new StudyCoursePlan
            {
                SessionType = "Group",
                Capacity = policy.MaxLearners,
                Name = $"Lớp nhóm JLPT {levelCode} · {weeklySessions} buổi/tuần ({totalSessions} buổi)",
                CertificateLevelId = levelId,
                DurationMonths = Math.Max(1, (int)Math.Ceiling(durationWeeks / 4.0)),
                SessionsPerWeek = weeklySessions,
                SessionMinutes = sessionMinutes,
                TotalSessions = totalSessions,
                RetakeClaimDays = 30,
                Price = totalTuition,
                IsPublished = true,
                Revision = 1,
            };
            await unitOfWork.StudyCoursePlans.AddAsync(plan, token);
            await unitOfWork.SaveChangesAsync(token);
        }

        if (proposal.CoursePlanId != plan.Id)
        {
            proposal.CoursePlanId = plan.Id;
            unitOfWork.MentorTeachingProposals.Update(proposal);
        }

        var rawCohortName = !string.IsNullOrWhiteSpace(proposal.CourseTitle)
            ? proposal.CourseTitle.Trim()
            : $"Lớp JLPT {levelCode} ({weeklySessions} buổi/tuần)";
        var cohortName = rawCohortName.Length <= 200 ? rawCohortName : rawCohortName[..200];

        var cohort = new StudyCohort
        {
            PlanId = plan.Id,
            Name = cohortName,
            MentorId = proposal.MentorId,
            StartsAt = orderedSlots[0].StartTime,
            EndsAt = orderedSlots[^1].EndTime,
            Capacity = policy.MaxLearners,
            CapacitySnapshot = policy.MaxLearners,
            GroupPolicyId = policy.Id,
            PolicyVersionSnapshot = policy.Version,
            SourceProposalId = proposal.Id,
            TuitionPerSessionSnapshot = tuitionPerSession,
            TotalTuitionSnapshot = totalTuition,
            TuitionPolicyVersionSnapshot = policy.Version,
            TuitionSnapshotAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            Status = "Published",
            Revision = 1,
        };

        for (var i = 0; i < orderedSlots.Count; i++)
        {
            cohort.Sessions.Add(new StudyCohortSession
            {
                Number = i + 1,
                AvailabilityId = orderedSlots[i].Id,
            });
        }

        await unitOfWork.StudyCohorts.AddAsync(cohort, token);
        await unitOfWork.SaveChangesAsync(token);
        return cohort;
    }

    // ───────────────────────────── CORE VALIDATION ENGINE ─────────────────────────────

    private sealed record ProposalCandidate(
        int? ProposalId,
        int MentorId,
        int? CertificateLevelId,
        string ProgramType,
        int? CoursePlanId,
        string? CourseObjective,
        DateOnly? ExpectedStartDate,
        int CourseDurationWeeks,
        int SessionDurationMinutes,
        List<WeeklySessionSlotDto> WeeklySessions,
        List<ProposalAttachmentDto> OptionalAttachments);

    private async Task<TeachingProposalValidationResponse> EvaluateProposalAsync(
        ProposalCandidate candidate,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        CancellationToken ct,
        bool isApprovalOrPublication = false)
    {
        var issues = new List<ProposalValidationIssue>();
        var allowedCounts = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv);
        var minPerWeek = allowedCounts.Min();
        var maxPerWeek = allowedCounts.Max();
        var restMinutes = Math.Max(0, policy.RestMinutes);
        var requiredSessionMinutes = policy.SessionMinutes;
        var requiredWeeks = policy.CourseDurationWeeks;
        var minProposalLeadDays = policy.MinimumProposalLeadDays > 0
            ? policy.MinimumProposalLeadDays
            : MentorSlotPolicy.MinimumProposalLeadDays;
        var minEnrollmentLeadDays = policy.MinimumPublishedEnrollmentDays > 0
            ? policy.MinimumPublishedEnrollmentDays
            : MentorSlotPolicy.MinimumPublishedEnrollmentDays;
        string? resolvedLevelCode = null;

        // 1. Mentor verification, active account & verified teaching qualification check
        var mentor = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Id == candidate.MentorId && u.Role == Role.Mentor && !u.IsDeleted, ct);
        var mentorVerified = mentor is not null && mentor.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved;
        var verifiedQualCodes = await LoadMentorQualifiedLevelCodesAsync(candidate.MentorId, ct);
        var highestVerifiedRank = ResolveHighestVerifiedRank(mentor?.MentorProfile, verifiedQualCodes);
        var highestVerifiedCode = MentorSlotPolicy.GetJlptCodeFromRank(highestVerifiedRank);

        if (!mentorVerified)
        {
            issues.Add(Error("MENTOR_QUALIFICATION_NOT_VERIFIED",
                "Hồ sơ chuyên môn của Giáo viên cần được xác minh trước khi gửi đề xuất mở lớp."));
        }
        else if (highestVerifiedRank <= 0)
        {
            issues.Add(Error("MENTOR_QUALIFICATION_NOT_VERIFIED",
                "Giáo viên chưa có chứng chỉ JLPT được xác minh hợp lệ để đăng ký mở lớp."));
        }

        // 2. JLPT level existence, N5–N2 scope, qualification ranking & per-level / total active limit protection
        List<int>? excludeIds = candidate.ProposalId.HasValue ? [candidate.ProposalId.Value] : null;
        var (blockingMap, _, totalActiveCount, maxTotalActiveClasses) =
            await BuildLevelBlockingMapAsync(candidate.MentorId, excludeIds, policy, nowUtc, ct);
        var totalLimitReached = maxTotalActiveClasses > 0 && totalActiveCount >= maxTotalActiveClasses;

        if (!candidate.CertificateLevelId.HasValue || candidate.CertificateLevelId.Value <= 0)
        {
            var isNewOrDraft = !candidate.ProposalId.HasValue
                || await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                    .AnyAsync(p => p.Id == candidate.ProposalId.Value && p.Status == MentorTeachingProposalStatus.Draft, ct);
            if (isNewOrDraft && mentorVerified && highestVerifiedRank > 0)
            {
                if (totalLimitReached)
                {
                    issues.Add(Error("MENTOR_TOTAL_CLASS_LIMIT_REACHED",
                        $"Bạn đã đạt giới hạn tối đa {maxTotalActiveClasses} lớp/đề xuất đang hoạt động ({totalActiveCount}/{maxTotalActiveClasses})."));
                }
                else
                {
                    var supportedLevels = await unitOfWork.CertificateLevels.Query().AsNoTracking()
                        .Include(l => l.CertificateType)
                        .Where(l => !l.IsDeleted && l.CertificateType != null && !l.CertificateType.IsDeleted && l.CertificateType.IsActive)
                        .Select(l => new { l.Id, l.Code })
                        .ToListAsync(ct);
                    var qualifiedIds = supportedLevels
                        .Where(l => SupportedClassJlptCodes.Contains((l.Code ?? string.Empty).Trim())
                            && MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, l.Code))
                        .Select(l => l.Id)
                        .ToList();
                    if (qualifiedIds.Count > 0 && qualifiedIds.All(id => blockingMap.ContainsKey(id)))
                    {
                        issues.Add(Error("MENTOR_ALL_LEVELS_OCCUPIED", "Bạn đã sử dụng hết hạn mức đăng ký ở tất cả các cấp độ đủ điều kiện giảng dạy."));
                    }
                }
            }
            issues.Add(Error("PROPOSAL_LEVEL_REQUIRED", "Vui lòng chọn trình độ JLPT dự kiến giảng dạy."));
        }
        else
        {
            var levelRow = await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Include(l => l.CertificateType)
                .Where(l => l.Id == candidate.CertificateLevelId.Value
                    && !l.IsDeleted
                    && l.CertificateType != null
                    && !l.CertificateType.IsDeleted
                    && l.CertificateType.IsActive)
                .Select(l => new { l.Id, l.Code })
                .FirstOrDefaultAsync(ct);

            if (levelRow is null)
            {
                issues.Add(Error("PROPOSAL_LEVEL_INVALID", "Trình độ JLPT đã chọn không tồn tại hoặc không còn hoạt động."));
            }
            else if (!MentorSlotPolicy.IsSupportedClassLevel(levelRow.Code))
            {
                issues.Add(Error(
                    "PROPOSAL_LEVEL_NOT_SUPPORTED",
                    $"Hiện tại hệ thống chỉ hỗ trợ đăng ký mở lớp nhóm ở các cấp độ N5–N2 (cấp độ {levelRow.Code} không thuộc phạm vi mở lớp)."));
            }
            else
            {
                resolvedLevelCode = levelRow.Code;
                if (mentorVerified && highestVerifiedRank > 0 && !MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, levelRow.Code))
                {
                    issues.Add(Error(
                        "INSUFFICIENT_TEACHING_QUALIFICATION",
                        $"Không đủ trình độ chuyên môn. Chứng chỉ đã xác minh ({highestVerifiedCode}) chỉ đủ điều kiện giảng dạy từ N5 đến {(highestVerifiedRank >= 4 ? "N2" : highestVerifiedCode)}."));
                }

                if (policy.GetTuitionPerSession(levelRow.Code) <= 0)
                {
                    issues.Add(Error(
                        "TUITION_NOT_CONFIGURED",
                        $"Học phí cho trình độ JLPT {levelRow.Code} chưa được cấu hình hợp lệ. Vui lòng liên hệ Quản trị viên cấu hình đơn giá trước khi mở lớp."));
                }

                if (blockingMap.TryGetValue(levelRow.Id, out var levelBlock))
                {
                    var formattedReason = FormatLevelBlockingReason(levelRow.Code, levelBlock);
                    var limitCode = levelBlock.Status switch
                    {
                        "LevelDisabled" => "PROPOSAL_LEVEL_DISABLED",
                        "TotalClassLimit" => "MENTOR_TOTAL_CLASS_LIMIT_REACHED",
                        _ => "PROPOSAL_DUPLICATE_ACTIVE_LEVEL",
                    };
                    issues.Add(new ProposalValidationIssue
                    {
                        Code = limitCode,
                        Severity = "Error",
                        Message = $"{formattedReason}{(string.IsNullOrWhiteSpace(levelBlock.Title) ? "" : $" ({levelBlock.Title})")}",
                        ConflictSource = levelBlock.Title,
                        ConflictJlptLevel = levelRow.Code,
                        ConflictStatus = levelBlock.Status,
                        ConflictEntityType = levelBlock.CohortId.HasValue ? "StudyCohort" : "TeachingProposal",
                        ConflictProposalId = levelBlock.ProposalId,
                        ConflictCohortId = levelBlock.CohortId,
                    });
                }
            }
        }

        // 3. Program & Course Plan / Objective check
        StudyCoursePlan? plan = null;
        if (candidate.CoursePlanId.HasValue && candidate.CoursePlanId.Value > 0)
        {
            plan = await unitOfWork.StudyCoursePlans.Query().AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == candidate.CoursePlanId.Value, ct);
            if (plan is null || !string.Equals(plan.SessionType, "Group", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("PROPOSAL_PLAN_MISMATCH", "Gói khóa học đã chọn không tồn tại hoặc không phải gói học nhóm."));
            }
            else
            {
                if (candidate.CertificateLevelId.HasValue && plan.CertificateLevelId != candidate.CertificateLevelId.Value)
                    issues.Add(Error("PROPOSAL_PLAN_MISMATCH", "Gói khóa học không khớp với trình độ JLPT đã chọn."));

                if (plan.SessionMinutes.HasValue && plan.SessionMinutes.Value != requiredSessionMinutes)
                    issues.Add(Error("PROPOSAL_PLAN_MISMATCH", $"Thời lượng buổi của gói ({plan.SessionMinutes} phút) không khớp quy chuẩn {requiredSessionMinutes} phút."));

                if (candidate.WeeklySessions.Count > 0 && plan.SessionsPerWeek != candidate.WeeklySessions.Count)
                    issues.Add(Error("PROPOSAL_PLAN_MISMATCH", $"Gói \"{plan.Name}\" yêu cầu đúng {plan.SessionsPerWeek} buổi/tuần (hiện chọn {candidate.WeeklySessions.Count} buổi/tuần)."));
            }
        }

        if (string.IsNullOrWhiteSpace(candidate.CourseObjective))
        {
            issues.Add(Error("PROPOSAL_OBJECTIVE_REQUIRED", "Vui lòng nhập mục tiêu khóa học."));
        }

        // 4. Duration & session length check
        if (candidate.CourseDurationWeeks != requiredWeeks)
            issues.Add(Error("PROPOSAL_DURATION_MISMATCH", $"Thời lượng khóa chuẩn theo chính sách hiện hành là {requiredWeeks} tuần."));

        if (candidate.SessionDurationMinutes != requiredSessionMinutes)
            issues.Add(Error("PROPOSAL_SESSION_MINUTES_MISMATCH", $"Mỗi buổi dạy phải có thời lượng đúng {requiredSessionMinutes} phút."));

        // 5. Weekly session count (2–4 sessions/week per proposal)
        var sessions = candidate.WeeklySessions;
        if (sessions.Count < minPerWeek)
        {
            issues.Add(Error("PROPOSAL_MIN_SESSIONS_NOT_MET",
                $"Mỗi đề xuất mở lớp cần tối thiểu {minPerWeek} buổi/tuần (hiện có {sessions.Count} buổi/tuần)."));
        }
        else if (sessions.Count > maxPerWeek)
        {
            issues.Add(Error("PROPOSAL_MAX_SESSIONS_EXCEEDED",
                $"Mỗi đề xuất mở lớp chỉ được chọn tối đa {maxPerWeek} buổi/tuần (hiện có {sessions.Count} buổi/tuần)."));
        }
        else if (!allowedCounts.Contains(sessions.Count))
        {
            issues.Add(Error("PROPOSAL_UNSUPPORTED_WEEKLY_COUNT",
                $"Số buổi mỗi tuần phải nằm trong danh sách cho phép ({string.Join(", ", allowedCounts)} buổi/tuần)."));
        }

        // 6. Individual slot integrity + own overlap + own rest
        var seenSlotKeys = new HashSet<(int, int)>();
        foreach (var s in sessions)
        {
            if (s.Weekday is < 0 or > 6)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", "Thứ trong tuần không hợp lệ.", s.Weekday, s.StartMinute));
            else if (s.EndMinute - s.StartMinute != requiredSessionMinutes)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Buổi {WeekdayLabel(s.Weekday)} phải có thời lượng đúng {requiredSessionMinutes} phút.", s.Weekday, s.StartMinute));
            else if (s.StartMinute < DefaultOperatingStartMinute || s.EndMinute > DefaultOperatingEndMinute)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Buổi {WeekdayLabel(s.Weekday)} ({Clock(s.StartMinute)}–{Clock(s.EndMinute)}) nằm ngoài khung giờ giảng dạy 07:00–22:00.", s.Weekday, s.StartMinute));
            else if (s.StartMinute % DefaultGridStepMinutes != 0)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Giờ bắt đầu {Clock(s.StartMinute)} phải chẵn theo bước {DefaultGridStepMinutes} phút.", s.Weekday, s.StartMinute));
            else if (!seenSlotKeys.Add((s.Weekday, s.StartMinute)))
                issues.Add(Error("PROPOSAL_OWN_OVERLAP", $"Trùng lặp khung giờ {WeekdayLabel(s.Weekday)} {Clock(s.StartMinute)}–{Clock(s.EndMinute)} trong cùng đề xuất.", s.Weekday, s.StartMinute));
        }

        foreach (var dayGroup in sessions.GroupBy(s => s.Weekday))
        {
            var ordered = dayGroup.OrderBy(s => s.StartMinute).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var curr = ordered[i];
                if (curr.StartMinute < prev.EndMinute)
                {
                    issues.Add(Error("PROPOSAL_OWN_OVERLAP",
                        $"Hai buổi trong {WeekdayLabel(dayGroup.Key)} bị chồng chéo ({Clock(prev.StartMinute)}–{Clock(prev.EndMinute)} và {Clock(curr.StartMinute)}–{Clock(curr.EndMinute)}).",
                        curr.Weekday, curr.StartMinute));
                }
                else if (curr.StartMinute - prev.EndMinute < restMinutes)
                {
                    issues.Add(Error("PROPOSAL_OWN_REST_VIOLATION",
                        $"Hai buổi trong {WeekdayLabel(dayGroup.Key)} quá sát nhau ({Clock(prev.StartMinute)}–{Clock(prev.EndMinute)} và {Clock(curr.StartMinute)}–{Clock(curr.EndMinute)}); cần nghỉ tối thiểu {restMinutes} phút.",
                        curr.Weekday, curr.StartMinute));
                }
            }
        }

        // 7. Expected start date, lead-time policy & 10-week schedule expansion (anchored strictly at expectedStartDate)
        var occurrences = new List<GeneratedSessionOccurrenceResponse>();
        DateOnly? projectedEndDate = null;
        var todayVn = DateOnly.FromDateTime(LocalTime(nowUtc));
        var minProposalStartDate = todayVn.AddDays(minProposalLeadDays);
        var minEnrollmentStartDate = todayVn.AddDays(minEnrollmentLeadDays);
        var hasStructuralSlotError = issues.Any(i => i.Code is "PROPOSAL_INVALID_SESSION_SLOT" or "PROPOSAL_OWN_OVERLAP");

        if (!candidate.ExpectedStartDate.HasValue)
        {
            issues.Add(Error("PROPOSAL_START_DATE_REQUIRED", "Vui lòng chọn ngày bắt đầu dự kiến."));

            if (sessions.Count > 0 && !hasStructuralSlotError)
            {
                var prelimStart = FindEarliestValidStartDate(minProposalStartDate, sessions);
                var prelimOccurrences = ExpandOccurrences(prelimStart, sessions, requiredWeeks, requiredSessionMinutes, DateTime.MinValue);
                if (prelimOccurrences.Count > 0)
                {
                    var prelimIntervals = prelimOccurrences
                        .Select(o => new ProposedTeachingInterval(o.StartUtc, o.EndUtc, o.SessionNumber))
                        .ToList();
                    var prelimReport = await teachingConflictService.CheckAsync(
                        candidate.MentorId,
                        prelimIntervals,
                        new TeachingConflictOptions
                        {
                            ExcludeProposalId = candidate.ProposalId,
                            RestMinutes = restMinutes,
                            DailyCap = policy.DailySessionCap,
                            WeeklyCap = policy.WeeklySessionCap,
                            IncludeOwnIntervals = false,
                        },
                        ct);

                    foreach (var c in prelimReport.Conflicts.Take(25))
                    {
                        var warn = MapConflictIssue(c, isPreliminaryWarning: true);
                        warn.Message = $"Cần kiểm tra theo ngày bắt đầu: {c.Message}";
                        issues.Add(warn);
                    }
                }
            }
        }
        else
        {
            var startDate = candidate.ExpectedStartDate.Value;
            if (startDate < todayVn)
            {
                issues.Add(Error("PAST_SCHEDULE_SLOT", "Ngày bắt đầu dự kiến không được ở trong quá khứ."));
            }
            else if (!isApprovalOrPublication && startDate < minProposalStartDate)
            {
                issues.Add(Error(
                    "INSUFFICIENT_PROPOSAL_LEAD_TIME",
                    $"Ngày bắt đầu cần cách thời điểm gửi đề xuất ít nhất {minProposalLeadDays} ngày."));
            }
            else if (isApprovalOrPublication && startDate < minEnrollmentStartDate)
            {
                issues.Add(Error(
                    "INSUFFICIENT_ENROLLMENT_LEAD_TIME",
                    "Chưa đủ thời gian tuyển sinh trước buổi học đầu tiên."));
            }

            var startMatchesWeekday = sessions.Count == 0 || sessions.Any(s => s.Weekday == (int)startDate.DayOfWeek);
            if (!startMatchesWeekday)
            {
                issues.Add(Error(
                    "COURSE_START_SESSION_MISMATCH",
                    "Ngày bắt đầu dự kiến phải trùng với một thứ đã đăng ký trong lịch dạy hàng tuần."));
            }
            else if (startDate >= todayVn && sessions.Count > 0 && !hasStructuralSlotError)
            {
                // Luôn dùng requiredWeeks từ chính sách máy chủ (không tin durationWeeks tự ý từ client).
                occurrences = ExpandOccurrences(startDate, sessions, requiredWeeks, requiredSessionMinutes, nowUtc);
                var expectedTotal = sessions.Count * requiredWeeks;
                if (occurrences.Count != expectedTotal)
                {
                    issues.Add(Error("INVALID_RECURRENCE_SCHEDULE",
                        $"Không thể sinh đủ {expectedTotal} buổi học trong {requiredWeeks} tuần liên tiếp."));
                }
                else if (occurrences.Count > 0)
                {
                    var expectedStartIso = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (!string.Equals(occurrences[0].LocalDate, expectedStartIso, StringComparison.Ordinal))
                    {
                        issues.Add(Error("COURSE_START_SESSION_MISMATCH",
                            "Ngày bắt đầu dự kiến không khớp với ngày diễn ra buổi học đầu tiên."));
                    }

                    projectedEndDate = DateOnly.ParseExact(occurrences[^1].LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (occurrences[0].StartUtc <= nowUtc)
                    {
                        issues.Add(Error("SESSION_ALREADY_STARTED",
                            "Buổi học đầu tiên đã trôi qua hoặc đã bắt đầu. Vui lòng chọn ngày bắt đầu muộn hơn."));
                    }
                }
            }
        }

        // 8. Authoritative cross-schedule conflict & Mentor-wide workload check across full 10-week horizon
        if (occurrences.Count > 0)
        {
            var proposedIntervals = occurrences
                .Select(o => new ProposedTeachingInterval(o.StartUtc, o.EndUtc, o.SessionNumber))
                .ToList();

            // MentorTeachingConflictService kiểm tra đồng bộ: StudyCohorts đang mở/nháp, MentorAvailabilities đã duyệt từ proposal khác, PendingReview và ChangesRequested proposals (bỏ qua Cancelled cohorts).
            var conflictReport = await teachingConflictService.CheckAsync(
                candidate.MentorId,
                proposedIntervals,
                new TeachingConflictOptions
                {
                    ExcludeProposalId = candidate.ProposalId,
                    RestMinutes = restMinutes,
                    DailyCap = policy.DailySessionCap,
                    WeeklyCap = policy.WeeklySessionCap,
                    IncludeOwnIntervals = false,
                },
                ct);

            foreach (var c in conflictReport.Conflicts)
            {
                issues.Add(MapConflictIssue(c, isPreliminaryWarning: false));
            }
        }

        var workload = await ComputeMentorWorkloadAsync(candidate.MentorId, policy, nowUtc, ct);
        var hasErrors = issues.Any(i => i.Severity == "Error");
        var totalSessions = occurrences.Count > 0 ? occurrences.Count : sessions.Count * candidate.CourseDurationWeeks;
        var tuitionPerSession = !string.IsNullOrWhiteSpace(resolvedLevelCode)
            ? policy.GetTuitionPerSession(resolvedLevelCode)
            : 0m;
        var totalCourseTuition = tuitionPerSession * totalSessions;

        return new TeachingProposalValidationResponse
        {
            IsValid = !hasErrors,
            CanSubmit = !hasErrors && sessions.Count >= minPerWeek && sessions.Count <= maxPerWeek && occurrences.Count > 0,
            WeeklySessionCount = sessions.Count,
            TotalSessions = totalSessions,
            TotalTeachingMinutes = totalSessions * candidate.SessionDurationMinutes,
            TuitionPerSession = tuitionPerSession,
            TotalCourseTuition = totalCourseTuition,
            TuitionPolicyVersion = tuitionPerSession > 0 ? policy.Version : null,
            ProjectedEndDate = projectedEndDate,
            Issues = issues,
            OccurrencesPreview = occurrences,
            Workload = workload,
        };
    }

    // ───────────────────────────── HELPERS ─────────────────────────────

    private IQueryable<MentorTeachingProposal> QueryProposalsWithIncludes(bool includeEvents = false)
    {
        IQueryable<MentorTeachingProposal> query = unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Include(p => p.Mentor).ThenInclude(u => u.MentorProfile)
            .Include(p => p.CertificateLevel)
            .Include(p => p.CoursePlan)
            .Include(p => p.ReviewedByAdmin)
            .Include(p => p.GeneratedAvailabilities);

        if (includeEvents)
            query = query.Include(p => p.Events);

        return query.AsSplitQuery();
    }

    private async Task<MentorWorkloadSummaryResponse> ComputeMentorWorkloadAsync(
        int mentorId, MentorGroupPolicy policy, DateTime nowUtc, CancellationToken ct)
    {
        var activeCohorts = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Include(c => c.Plan)
            .Where(c => c.MentorId == mentorId && (c.Status == "Published" || c.Status == "Draft") && c.EndsAt > nowUtc)
            .Select(c => new { c.Id, c.SourceProposalId, c.Plan.SessionsPerWeek })
            .ToListAsync(ct);

        var linkedProposalIds = activeCohorts
            .Where(c => c.SourceProposalId.HasValue)
            .Select(c => c.SourceProposalId!.Value)
            .ToHashSet();

        var cancelledCohortAvIds = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Where(c => c.MentorId == mentorId && c.Status == "Cancelled")
            .SelectMany(c => c.Sessions.Select(s => s.AvailabilityId))
            .ToListAsync(ct);
        var cancelledAvSet = cancelledCohortAvIds.ToHashSet();

        var proposals = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Where(p => p.MentorId == mentorId)
            .Select(p => new
            {
                p.Id,
                p.Status,
                p.WeeklySessionsJson,
                HasFutureApprovedSlots = p.Status == MentorTeachingProposalStatus.Approved
                    && p.GeneratedAvailabilities.Any(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null && a.EndTime > nowUtc && !cancelledCohortAvIds.Contains(a.Id)),
            })
            .ToListAsync(ct);

        var pendingProps = proposals.Where(p => p.Status == MentorTeachingProposalStatus.PendingReview).ToList();
        var changesRequestedProps = proposals.Where(p => p.Status == MentorTeachingProposalStatus.ChangesRequested).ToList();
        var activeApprovedUnlinkedProps = proposals.Where(p =>
            p.Status == MentorTeachingProposalStatus.Approved
            && !linkedProposalIds.Contains(p.Id)
            && p.HasFutureApprovedSlots).ToList();

        var pendingWeeklySessions = pendingProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count)
            + changesRequestedProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count);
        var approvedUnlinkedWeekly = activeApprovedUnlinkedProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count);
        var approvedCount = activeApprovedUnlinkedProps.Count;
        var draftCount = proposals.Any(p => p.Status == MentorTeachingProposalStatus.Draft && DeserializeSessions(p.WeeklySessionsJson).Count > 0) ? 1 : 0;

        var weekStart = WeekStartUtc(nowUtc);
        var horizonEnd = weekStart.AddDays(70);
        var upcomingSlots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Where(a => a.MentorId == mentorId && !a.IsDeleted && a.RejectedAt == null && a.IsApproved
                && a.StartTime >= weekStart && a.StartTime < horizonEnd)
            .Select(a => new { a.Id, a.StartTime })
            .ToListAsync(ct);

        var peakDay = upcomingSlots
            .Where(a => !cancelledAvSet.Contains(a.Id))
            .GroupBy(a => DateOnly.FromDateTime(LocalTime(MentorTeachingConflictService.Utc(a.StartTime))))
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .Max();

        var weeklyCommitted = activeCohorts.Sum(c => c.SessionsPerWeek) + approvedUnlinkedWeekly;

        return new MentorWorkloadSummaryResponse
        {
            ActiveCohortsCount = activeCohorts.Count,
            PendingProposalsCount = pendingProps.Count,
            ChangesRequestedProposalsCount = changesRequestedProps.Count,
            ApprovedProposalsCount = approvedCount,
            DraftProposalsCount = draftCount,
            WeeklyCommittedSessions = weeklyCommitted,
            WeeklyPendingProposalSessions = pendingWeeklySessions,
            WeeklyTotalProjectedSessions = weeklyCommitted + pendingWeeklySessions,
            PeakDaySessions = peakDay,
            DailySessionCap = policy.DailySessionCap,
            WeeklySessionCap = policy.WeeklySessionCap,
        };
    }

    private sealed record LevelBlockInfo(string Status, string Reason, int? ProposalId, int? CohortId, string? Title);

    private sealed record MentorCapacitySnapshot(
        Dictionary<int, LevelBlockInfo> BlockingMap,
        Dictionary<int, int> ActiveCountByLevel,
        int TotalActiveCount,
        int MaxTotalActiveClasses);

    private async Task<(string Code, string Message)?> CheckMentorNewRegistrationCapacityAsync(
        int mentorId, MentorGroupPolicy policy, DateTime nowUtc, CancellationToken ct)
    {
        var mentor = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, ct);
        if (mentor?.MentorProfile?.VerificationStatus != MentorVerificationStatus.Approved)
            return null;

        var verifiedQualCodes = await LoadMentorQualifiedLevelCodesAsync(mentorId, ct);
        var highestVerifiedRank = ResolveHighestVerifiedRank(mentor.MentorProfile, verifiedQualCodes);
        if (highestVerifiedRank <= 0)
            return null;

        var activeLevelsRaw = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Include(l => l.CertificateType)
            .Where(l => !l.IsDeleted && l.CertificateType != null && !l.CertificateType.IsDeleted && l.CertificateType.IsActive)
            .Select(l => new { l.Id, l.Code })
            .ToListAsync(ct);

        var qualifiedIds = activeLevelsRaw
            .Where(l => SupportedClassJlptCodes.Contains((l.Code ?? string.Empty).Trim())
                && MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, l.Code))
            .Select(l => l.Id)
            .ToList();

        if (qualifiedIds.Count == 0)
            return null;

        var snapshot = await BuildLevelBlockingMapAsync(mentorId, excludeProposalIds: null, policy, nowUtc, ct);
        if (snapshot.MaxTotalActiveClasses > 0 && snapshot.TotalActiveCount >= snapshot.MaxTotalActiveClasses)
        {
            return (
                "MENTOR_TOTAL_CLASS_LIMIT_REACHED",
                $"Bạn đã đạt giới hạn tối đa {snapshot.MaxTotalActiveClasses} lớp/đề xuất đang hoạt động ({snapshot.TotalActiveCount}/{snapshot.MaxTotalActiveClasses}).");
        }

        if (qualifiedIds.All(id => snapshot.BlockingMap.ContainsKey(id)))
        {
            return ("MENTOR_ALL_LEVELS_OCCUPIED", "Bạn đã sử dụng hết hạn mức đăng ký ở tất cả các cấp độ đủ điều kiện giảng dạy.");
        }

        return null;
    }

    private async Task<MentorCapacitySnapshot> BuildLevelBlockingMapAsync(
        int mentorId, IReadOnlyCollection<int>? excludeProposalIds, MentorGroupPolicy policy, DateTime nowUtc, CancellationToken ct)
    {
        var excludedIds = excludeProposalIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];
        var map = new Dictionary<int, LevelBlockInfo>();
        var firstBlockerByLevel = new Dictionary<int, LevelBlockInfo>();
        var activeCountByLevel = new Dictionary<int, int>();

        var supportedLevelsRaw = (await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Include(l => l.CertificateType)
            .Where(l => !l.IsDeleted && l.CertificateType != null && !l.CertificateType.IsDeleted && l.CertificateType.IsActive)
            .Select(l => new { l.Id, l.Code })
            .ToListAsync(ct))
            .Where(l => SupportedClassJlptCodes.Contains((l.Code ?? string.Empty).Trim()))
            .ToList();

        var activeProposals = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Where(p => p.MentorId == mentorId
                && (excludedIds.Count == 0 || !excludedIds.Contains(p.Id))
                && p.CertificateLevelId.HasValue
                && (p.Status == MentorTeachingProposalStatus.PendingReview
                    || p.Status == MentorTeachingProposalStatus.ChangesRequested
                    || p.Status == MentorTeachingProposalStatus.Approved))
            .Select(p => new
            {
                p.Id,
                LevelId = p.CertificateLevelId!.Value,
                p.Status,
                p.CourseTitle,
                HasFutureSlots = p.GeneratedAvailabilities.Any(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null && a.EndTime > nowUtc),
            })
            .ToListAsync(ct);

        var allMentorCohorts = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Include(c => c.Plan)
            .Include(c => c.Enrollments)
            .Where(c => c.MentorId == mentorId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.StartsAt,
                c.EndsAt,
                c.SourceProposalId,
                LevelId = c.Plan.CertificateLevelId,
                HasConfirmedEnrollments = c.Enrollments.Any(e =>
                    (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                    && e.Status != "PaymentReview"
                    && e.Status != "Cancelled"
                    && e.Status != "Transferred"
                    && e.Status != "PendingPayment"
                    && e.Status != "PaymentExpired"
                    && e.Status != "PaymentFailed"),
                HasIncompleteConfirmedEnrollment = c.Enrollments.Any(e =>
                    (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded")
                    && e.Status != "PaymentReview"
                    && e.Status != "Cancelled"
                    && e.Status != "Transferred"
                    && e.Status != "PendingPayment"
                    && e.Status != "PaymentExpired"
                    && e.Status != "PaymentFailed"
                    && e.Status != "Completed"),
            })
            .ToListAsync(ct);

        static bool IsCohortEnded(string? status, DateTime endsAt, bool hasConfirmed, bool hasIncompleteConfirmed, DateTime utcNow)
        {
            var s = status?.Trim() ?? string.Empty;
            if (string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase))
                return true;
            if (hasConfirmed && !hasIncompleteConfirmed)
                return true;
            // Lớp chưa có học viên nhưng đã kết thúc toàn bộ lịch học -> không tiếp tục khóa cấp độ của Giáo viên.
            return !hasConfirmed && endsAt <= utcNow;
        }

        var allCohortByProposalId = allMentorCohorts
            .Where(c => c.SourceProposalId.HasValue)
            .GroupBy(c => c.SourceProposalId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var activeCohorts = allMentorCohorts
            .Where(c => !IsCohortEnded(c.Status, c.EndsAt, c.HasConfirmedEnrollments, c.HasIncompleteConfirmedEnrollment, nowUtc))
            .ToList();

        var activeCohortByProposalId = activeCohorts
            .Where(c => c.SourceProposalId.HasValue)
            .GroupBy(c => c.SourceProposalId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var countedProposalIds = new HashSet<int>();

        // Ưu tiên đếm lớp đang hoạt động / đang tuyển sinh / sắp bắt đầu / đang giảng dạy trước (không tính trùng đề xuất gốc)
        foreach (var c in activeCohorts)
        {
            if (c.SourceProposalId.HasValue)
                countedProposalIds.Add(c.SourceProposalId.Value);
            activeCountByLevel[c.LevelId] = activeCountByLevel.GetValueOrDefault(c.LevelId) + 1;
            if (!firstBlockerByLevel.ContainsKey(c.LevelId))
            {
                var status = c.Status == "Published" && c.StartsAt > nowUtc ? "RecruitingCohort" : "ActiveCohort";
                var reason = status == "RecruitingCohort"
                    ? "Bạn đã có lớp đang tuyển sinh."
                    : "Bạn đã có lớp đang hoạt động.";
                firstBlockerByLevel[c.LevelId] = new LevelBlockInfo(status, reason, c.SourceProposalId, c.Id, c.Name);
            }
        }

        // Sau đó đến các đề xuất còn hiệu lực (chưa gắn lớp đã kết thúc/hủy)
        foreach (var p in activeProposals)
        {
            if (p.Status == MentorTeachingProposalStatus.PendingReview)
            {
                activeCountByLevel[p.LevelId] = activeCountByLevel.GetValueOrDefault(p.LevelId) + 1;
                firstBlockerByLevel.TryAdd(p.LevelId, new LevelBlockInfo("PendingReview", "Bạn đã có đề xuất đang chờ duyệt.", p.Id, null, p.CourseTitle));
            }
            else if (p.Status == MentorTeachingProposalStatus.ChangesRequested)
            {
                activeCountByLevel[p.LevelId] = activeCountByLevel.GetValueOrDefault(p.LevelId) + 1;
                firstBlockerByLevel.TryAdd(p.LevelId, new LevelBlockInfo("ChangesRequested", "Bạn đã có đề xuất cần chỉnh sửa bổ sung.", p.Id, null, p.CourseTitle));
            }
            else if (p.Status == MentorTeachingProposalStatus.Approved && !countedProposalIds.Contains(p.Id))
            {
                if (allCohortByProposalId.TryGetValue(p.Id, out var linkedCohort))
                {
                    if (IsCohortEnded(linkedCohort.Status, linkedCohort.EndsAt, linkedCohort.HasConfirmedEnrollments, linkedCohort.HasIncompleteConfirmedEnrollment, nowUtc))
                        continue;
                    activeCohortByProposalId.TryGetValue(p.Id, out var activeLinked);
                    activeCountByLevel[p.LevelId] = activeCountByLevel.GetValueOrDefault(p.LevelId) + 1;
                    firstBlockerByLevel.TryAdd(p.LevelId, new LevelBlockInfo(
                        "Approved",
                        "Bạn đã có đề xuất đã duyệt ở cấp độ này.",
                        p.Id,
                        activeLinked?.Id ?? linkedCohort.Id,
                        activeLinked?.Name ?? linkedCohort.Name ?? p.CourseTitle));
                }
                else if (p.HasFutureSlots)
                {
                    activeCountByLevel[p.LevelId] = activeCountByLevel.GetValueOrDefault(p.LevelId) + 1;
                    firstBlockerByLevel.TryAdd(p.LevelId, new LevelBlockInfo(
                        "Approved",
                        "Bạn đã có đề xuất đã duyệt ở cấp độ này.",
                        p.Id,
                        null,
                        p.CourseTitle));
                }
            }
        }

        var totalActiveCount = activeCountByLevel.Values.Sum();
        var maxTotal = Math.Max(1, policy.MaxTotalActiveClassesPerMentor);
        var totalLimitReached = totalActiveCount >= maxTotal;

        foreach (var lvl in supportedLevelsRaw)
        {
            var code = (lvl.Code ?? string.Empty).Trim().ToUpperInvariant();
            var levelLimit = policy.GetMaxActiveClassesForLevel(lvl.Id, code);
            var used = activeCountByLevel.GetValueOrDefault(lvl.Id);
            firstBlockerByLevel.TryGetValue(lvl.Id, out var blocker);

            if (levelLimit <= 0)
            {
                map[lvl.Id] = new LevelBlockInfo(
                    "LevelDisabled",
                    $"Cấp độ JLPT {code} hiện tạm ngưng nhận đăng ký mở lớp mới theo cấu hình hệ thống.",
                    null, null, null);
            }
            else if (totalLimitReached)
            {
                map[lvl.Id] = new LevelBlockInfo(
                    "TotalClassLimit",
                    $"Bạn đã đạt giới hạn tối đa {maxTotal} lớp/đề xuất đang hoạt động ({totalActiveCount}/{maxTotal}).",
                    blocker?.ProposalId, blocker?.CohortId, blocker?.Title);
            }
            else if (used >= levelLimit && blocker is not null)
            {
                map[lvl.Id] = levelLimit == 1
                    ? blocker
                    : new LevelBlockInfo(
                        "LevelLimitReached",
                        $"Bạn đã đạt giới hạn tối đa {levelLimit} lớp/đề xuất đang hoạt động ở cấp độ JLPT {code} ({used}/{levelLimit}).",
                        blocker.ProposalId, blocker.CohortId, blocker.Title);
            }
        }

        return new MentorCapacitySnapshot(map, activeCountByLevel, totalActiveCount, maxTotal);
    }

    private static ProposalValidationIssue MapConflictIssue(TeachingConflict c, bool isPreliminaryWarning)
    {
        var mappedCode = c.Code switch
        {
            MentorTeachingConflictService.RestViolation => "PROPOSAL_REST_VIOLATION",
            MentorTeachingConflictService.DailyLimit => "PROPOSAL_DAILY_CAP_EXCEEDED",
            MentorTeachingConflictService.WeeklyLimit => "PROPOSAL_WEEKLY_CAP_EXCEEDED",
            _ when c.CommitmentStatus == MentorTeachingProposalStatus.PendingReview => "PROPOSAL_CONFLICT_PENDING_PROPOSAL",
            _ when c.CommitmentStatus == MentorTeachingProposalStatus.ChangesRequested => "PROPOSAL_CONFLICT_PENDING_PROPOSAL",
            _ => "PROPOSAL_CONFLICT_APPROVED_SCHEDULE",
        };

        var localStart = LocalTime(c.ProposedStartUtc);
        var conflictStart = c.CommitmentStartUtc.HasValue ? LocalTime(c.CommitmentStartUtc.Value) : (DateTime?)null;
        var conflictEnd = c.CommitmentEndUtc.HasValue ? LocalTime(c.CommitmentEndUtc.Value) : (DateTime?)null;

        return new ProposalValidationIssue
        {
            Code = mappedCode,
            Severity = isPreliminaryWarning ? "Warning" : "Error",
            Message = c.Message,
            Weekday = (int)localStart.DayOfWeek,
            StartMinute = localStart.Hour * 60 + localStart.Minute,
            OccurrenceStartUtc = c.ProposedStartUtc,
            OccurrenceEndUtc = c.ProposedEndUtc,
            ConflictSource = c.CohortName ?? c.CommitmentKind,
            ConflictDate = conflictStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ConflictLocalStart = conflictStart?.ToString("HH:mm", CultureInfo.InvariantCulture),
            ConflictLocalEnd = conflictEnd?.ToString("HH:mm", CultureInfo.InvariantCulture),
            ConflictJlptLevel = c.JlptLevelCode,
            ConflictStatus = c.CommitmentStatus,
            ConflictEntityType = c.CommitmentKind,
            ConflictProposalId = c.ProposalId,
            ConflictCohortId = c.CohortId,
        };
    }

    private static bool IsConflictOrCapacityCode(string? code) => code is
        "PROPOSAL_CONFLICT_APPROVED_SCHEDULE"
        or "PROPOSAL_CONFLICT_PENDING_PROPOSAL"
        or "PROPOSAL_REST_VIOLATION"
        or "PROPOSAL_OWN_OVERLAP"
        or "PROPOSAL_OWN_REST_VIOLATION"
        or "PROPOSAL_DUPLICATE_ACTIVE_LEVEL"
        or "PROPOSAL_LEVEL_DISABLED"
        or "MENTOR_TOTAL_CLASS_LIMIT_REACHED"
        or "MENTOR_ALL_LEVELS_OCCUPIED"
        or "PROPOSAL_DUPLICATE_DETECTED"
        or "PROPOSAL_DAILY_CAP_EXCEEDED"
        or "PROPOSAL_WEEKLY_CAP_EXCEEDED"
        or "PROPOSAL_MAX_SESSIONS_EXCEEDED";

    private static bool IsPgConflict(DbUpdateException ex)
        => ex.InnerException is System.Data.Common.DbException dbEx && dbEx.SqlState is "23505" or "23P01";

    private async Task<List<string>> LoadMentorQualifiedLevelCodesAsync(int mentorId, CancellationToken ct)
        => await unitOfWork.MentorTeachingQualifications.Query().AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Where(q => q.MentorUserId == mentorId && q.IsActive && q.RevokedAtUtc == null)
            .OrderBy(q => q.CertificateLevel.SortOrder)
            .Select(q => q.CertificateLevel.Code)
            .ToListAsync(ct);

    private async Task<Dictionary<int, List<string>>> LoadMentorsQualifiedLevelCodesAsync(List<int> mentorIds, CancellationToken ct)
    {
        if (mentorIds.Count == 0) return [];
        var rows = await unitOfWork.MentorTeachingQualifications.Query().AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Where(q => mentorIds.Contains(q.MentorUserId) && q.IsActive && q.RevokedAtUtc == null)
            .OrderBy(q => q.CertificateLevel.SortOrder)
            .Select(q => new { q.MentorUserId, q.CertificateLevel.Code })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.MentorUserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).Distinct().ToList());
    }

    private sealed record LinkedCohortSummary(
        int Id,
        string Name,
        string Status,
        decimal? TuitionPerSessionSnapshot,
        decimal? TotalTuitionSnapshot,
        int? TuitionPolicyVersionSnapshot);

    private async Task<Dictionary<int, LinkedCohortSummary>> LoadLinkedCohortsMapAsync(List<int> proposalIds, CancellationToken ct)
    {
        if (proposalIds.Count == 0) return [];
        var rows = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Where(c => c.SourceProposalId.HasValue && proposalIds.Contains(c.SourceProposalId.Value))
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                ProposalId = c.SourceProposalId!.Value,
                c.Id,
                c.Name,
                c.Status,
                c.TuitionPerSessionSnapshot,
                c.TotalTuitionSnapshot,
                c.TuitionPolicyVersionSnapshot,
            })
            .ToListAsync(ct);

        var dict = new Dictionary<int, LinkedCohortSummary>();
        foreach (var r in rows)
            dict.TryAdd(r.ProposalId, new LinkedCohortSummary(
                r.Id,
                r.Name,
                r.Status,
                r.TuitionPerSessionSnapshot,
                r.TotalTuitionSnapshot,
                r.TuitionPolicyVersionSnapshot));
        return dict;
    }

    private static TeachingProposalResponse MapProposal(
        MentorTeachingProposal p,
        MentorGroupPolicy policy,
        List<string> mentorQualifiedLevels,
        LinkedCohortSummary? linkedCohort,
        bool includeHistory)
    {
        var resolvedWeeks = p.CourseDurationWeeks > 0 ? p.CourseDurationWeeks : policy.CourseDurationWeeks;
        var resolvedMinutes = p.SessionDurationMinutes > 0 ? p.SessionDurationMinutes : policy.SessionMinutes;
        var sessions = DeserializeSessions(p.WeeklySessionsJson, resolvedMinutes);
        var attachments = DeserializeAttachments(p.OptionalAttachmentsJson);
        var totalSessions = sessions.Count * resolvedWeeks;
        var tuitionPerSession = linkedCohort?.TuitionPerSessionSnapshot is decimal snapPer && snapPer > 0
            ? snapPer
            : policy.GetTuitionPerSession(p.CertificateLevel?.Code);
        var totalCourseTuition = linkedCohort?.TotalTuitionSnapshot is decimal snapTotal && snapTotal > 0
            ? snapTotal
            : tuitionPerSession * totalSessions;
        var tuitionPolicyVersion = linkedCohort?.TuitionPolicyVersionSnapshot
            ?? (tuitionPerSession > 0 ? policy.Version : (int?)null);
        var canExpand = p.ExpectedStartDate.HasValue
            && sessions.Count > 0
            && sessions.Any(s => s.Weekday == (int)p.ExpectedStartDate.Value.DayOfWeek);
        var occurrences = canExpand
            ? ExpandOccurrences(p.ExpectedStartDate!.Value, sessions, resolvedWeeks, resolvedMinutes, DateTime.MinValue)
            : [];
        DateOnly? projectedEnd = occurrences.Count > 0
            ? DateOnly.ParseExact(occurrences[^1].LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

        var activeGeneratedIds = p.GeneratedAvailabilities
            .Where(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null)
            .OrderBy(a => a.StartTime)
            .Select(a => a.Id)
            .ToList();

        return new TeachingProposalResponse
        {
            Id = p.Id,
            MentorId = p.MentorId,
            MentorName = p.Mentor is null ? $"#{p.MentorId}" : $"{p.Mentor.FirstName} {p.Mentor.LastName}".Trim(),
            MentorEmail = p.Mentor?.Email ?? string.Empty,
            MentorVerified = p.Mentor?.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved,
            MentorVerificationStatus = p.Mentor?.MentorProfile?.VerificationStatus.ToString(),
            MentorJlptLevel = p.Mentor?.MentorProfile?.JlptLevel,
            MentorYearsOfExperience = p.Mentor?.MentorProfile?.YearsOfExperience,
            MentorExpertise = p.Mentor?.MentorProfile?.Expertise,
            MentorWorkplace = p.Mentor?.MentorProfile?.Workplace,
            MentorHasVerificationDocument = !string.IsNullOrWhiteSpace(p.Mentor?.MentorProfile?.VerificationDocumentUrl),
            MentorQualifiedLevels = mentorQualifiedLevels,
            CertificateLevelId = p.CertificateLevelId,
            JlptLevelCode = p.CertificateLevel?.Code,
            ProgramType = p.ProgramType,
            CoursePlanId = p.CoursePlanId,
            CoursePlanName = p.CoursePlan?.Name,
            CourseTitle = p.CourseTitle,
            CourseObjective = p.CourseObjective,
            ExpectedStartDate = p.ExpectedStartDate,
            ProjectedEndDate = projectedEnd,
            CourseDurationWeeks = resolvedWeeks,
            SessionDurationMinutes = resolvedMinutes,
            WeeklySessionCount = sessions.Count,
            TotalSessions = totalSessions,
            TuitionPerSession = tuitionPerSession,
            TotalCourseTuition = totalCourseTuition,
            TuitionPolicyVersion = tuitionPolicyVersion,
            WeeklySessions = sessions,
            OptionalNotes = p.OptionalNotes,
            OptionalAttachments = attachments,
            Status = p.Status,
            Revision = p.Revision,
            SubmittedAtUtc = p.SubmittedAtUtc,
            ReviewedAtUtc = p.ReviewedAtUtc,
            ReviewedByAdminId = p.ReviewedByAdminId,
            ReviewedByAdminName = p.ReviewedByAdmin is null ? null : $"{p.ReviewedByAdmin.FirstName} {p.ReviewedByAdmin.LastName}".Trim(),
            ReviewReason = p.ReviewReason,
            CreatedAtUtc = p.CreatedAtUtc,
            UpdatedAtUtc = p.UpdatedAtUtc,
            LastSavedAtUtc = p.LastSavedAtUtc,
            GeneratedAvailabilityIds = activeGeneratedIds,
            LinkedCohortId = linkedCohort is { Id: > 0 } lc ? lc.Id : null,
            LinkedCohortName = linkedCohort is { Id: > 0 } lc2 ? lc2.Name : null,
            LinkedCohortStatus = linkedCohort is { Id: > 0 } lc3 ? lc3.Status : null,
            OccurrencesPreview = occurrences,
            History = includeHistory
                ? p.Events.OrderByDescending(e => e.CreatedAtUtc).Select(e => new TeachingProposalEventResponse
                {
                    Id = e.Id,
                    ActorUserId = e.ActorUserId,
                    ActorRole = e.ActorRole,
                    Action = e.Action,
                    FromStatus = e.FromStatus,
                    ToStatus = e.ToStatus,
                    RevisionAfter = e.RevisionAfter,
                    Reason = e.Reason,
                    CreatedAtUtc = e.CreatedAtUtc,
                }).ToList()
                : [],
        };
    }

    private static TeachingProposalPolicyResponse MapPolicy(MentorGroupPolicy p)
    {
        var allowed = ParseAllowedSessionsPerWeek(p.AllowedSessionsPerWeekCsv);
        return new TeachingProposalPolicyResponse
        {
            PolicyId = p.Id,
            PolicyVersion = p.Version,
            TimeZoneId = MentorSlotPolicy.BusinessTimeZoneId,
            MinSessionsPerWeek = allowed.Min(),
            MaxSessionsPerWeek = allowed.Max(),
            DefaultSessionsPerWeek = p.DefaultSessionsPerWeek,
            AllowedSessionsPerWeek = allowed,
            CourseDurationWeeks = p.CourseDurationWeeks,
            SessionDurationMinutes = p.SessionMinutes,
            RestMinutes = p.RestMinutes,
            MinLearners = p.MinLearnersToStart,
            MaxLearners = p.MaxLearners,
            DailySessionCap = p.DailySessionCap,
            WeeklySessionCap = p.WeeklySessionCap,
            GridStepMinutes = DefaultGridStepMinutes,
            AllowedGridSteps = [15, 30],
            OperatingStartMinute = DefaultOperatingStartMinute,
            OperatingEndMinute = DefaultOperatingEndMinute,
            MinimumProposalLeadDays = p.MinimumProposalLeadDays,
            MinimumPublishedEnrollmentDays = p.MinimumPublishedEnrollmentDays,
            CheckoutHoldMinutes = p.CheckoutHoldMinutes,
            EnrollmentCutoffMinutesBeforeFirstSession = p.EnrollmentCutoffMinutesBeforeFirstSession,
            MaxTotalActiveClassesPerMentor = p.MaxTotalActiveClassesPerMentor,
            MaxActiveClassesByLevel = p.GetMaxActiveClassesByLevelCode(),
            TuitionPerSessionByLevel = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["N5"] = p.TuitionPerSessionN5,
                ["N4"] = p.TuitionPerSessionN4,
                ["N3"] = p.TuitionPerSessionN3,
                ["N2"] = p.TuitionPerSessionN2,
            },
        };
    }

    private static (string Code, string Message)? ValidateDraftFields(
        SaveTeachingProposalDraftRequest request,
        MentorGroupPolicy policy,
        out string normalizedProgram,
        out List<WeeklySessionSlotDto> normalizedSessions,
        out List<ProposalAttachmentDto> normalizedAttachments)
    {
        normalizedProgram = NormalizeProgramType(request.ProgramType);
        normalizedSessions = [];
        normalizedAttachments = [];

        var requiredSessionMinutes = policy.SessionMinutes > 0 ? policy.SessionMinutes : 90;
        var restMinutes = Math.Max(0, policy.RestMinutes);
        var rawSessions = (request.WeeklySessions ?? []).Where(s => s is not null).ToList();

        var seenKeys = new HashSet<(int, int)>();
        foreach (var raw in rawSessions)
        {
            var endMin = raw.EndMinute > 0 ? raw.EndMinute : raw.StartMinute + requiredSessionMinutes;
            if (raw.Weekday is < 0 or > 6)
                return ("PROPOSAL_INVALID_SESSION_SLOT", "Thứ trong tuần không hợp lệ.");
            if (endMin - raw.StartMinute != requiredSessionMinutes)
                return ("PROPOSAL_SESSION_MINUTES_MISMATCH", $"Mỗi buổi dạy phải có thời lượng đúng {requiredSessionMinutes} phút.");
            if (raw.StartMinute < DefaultOperatingStartMinute || endMin > DefaultOperatingEndMinute)
                return ("PROPOSAL_INVALID_SESSION_SLOT", $"Buổi {WeekdayLabel(raw.Weekday)} ({Clock(raw.StartMinute)}–{Clock(endMin)}) nằm ngoài khung giờ giảng dạy 07:00–22:00.");
            if (raw.StartMinute % DefaultGridStepMinutes != 0)
                return ("PROPOSAL_INVALID_SESSION_SLOT", $"Giờ bắt đầu {Clock(raw.StartMinute)} phải chẵn theo bước {DefaultGridStepMinutes} phút.");
            if (!seenKeys.Add((raw.Weekday, raw.StartMinute)))
                return ("PROPOSAL_OWN_OVERLAP", $"Trùng lặp khung giờ {WeekdayLabel(raw.Weekday)} {Clock(raw.StartMinute)}–{Clock(endMin)}.");

            normalizedSessions.Add(new WeeklySessionSlotDto
            {
                Weekday = raw.Weekday,
                StartMinute = raw.StartMinute,
                EndMinute = endMin,
            });
        }

        normalizedSessions = normalizedSessions
            .OrderBy(s => (s.Weekday + 6) % 7)
            .ThenBy(s => s.StartMinute)
            .ToList();

        var maxPerWeek = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv).Max();
        if (normalizedSessions.Count > maxPerWeek)
        {
            return ("PROPOSAL_MAX_SESSIONS_EXCEEDED",
                $"Mỗi đề xuất mở lớp chỉ được chọn tối đa {maxPerWeek} buổi/tuần.");
        }

        foreach (var dayGroup in normalizedSessions.GroupBy(s => s.Weekday))
        {
            var ordered = dayGroup.OrderBy(s => s.StartMinute).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var curr = ordered[i];
                if (curr.StartMinute < prev.EndMinute)
                {
                    return ("PROPOSAL_OWN_OVERLAP",
                        $"Hai buổi trong {WeekdayLabel(dayGroup.Key)} bị chồng chéo ({Clock(prev.StartMinute)}–{Clock(prev.EndMinute)} và {Clock(curr.StartMinute)}–{Clock(curr.EndMinute)}).");
                }
                if (curr.StartMinute - prev.EndMinute < restMinutes)
                {
                    return ("PROPOSAL_OWN_REST_VIOLATION",
                        $"Hai buổi trong {WeekdayLabel(dayGroup.Key)} cần nghỉ tối thiểu {restMinutes} phút.");
                }
            }
        }

        if (request.OptionalAttachments is not null)
        {
            if (request.OptionalAttachments.Count > MaxOptionalAttachments)
                return ("PROPOSAL_INVALID_ATTACHMENT", $"Tối đa {MaxOptionalAttachments} tệp đính kèm cho mỗi đề xuất.");

            foreach (var att in request.OptionalAttachments)
            {
                if (att is null || string.IsNullOrWhiteSpace(att.Name) || string.IsNullOrWhiteSpace(att.Url))
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Thông tin tệp đính kèm không hợp lệ.");

                var url = att.Url.Trim();
                string? extCandidate = null;

                var isSafeLocalPath = !url.Contains("..", StringComparison.Ordinal)
                    && LocalProposalAttachmentRegex.IsMatch(url);
                if (isSafeLocalPath)
                {
                    extCandidate = Path.GetExtension(url);
                }
                else if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                    && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp))
                {
                    extCandidate = Path.GetExtension(parsed.AbsolutePath);
                    if (string.IsNullOrWhiteSpace(extCandidate))
                        extCandidate = Path.GetExtension(att.StoredFileName ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(extCandidate))
                        extCandidate = Path.GetExtension(att.Name.Trim());
                }
                else
                {
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Đường dẫn tệp đính kèm không hợp lệ.");
                }

                if (string.IsNullOrWhiteSpace(extCandidate) || !AllowedAttachmentExtensions.Contains(extCandidate))
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Định dạng tệp đính kèm không được hỗ trợ.");

                if (att.SizeBytes is < 0 or > MaxAttachmentBytes)
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Dung lượng tệp đính kèm vượt quá giới hạn 25 MB.");

                normalizedAttachments.Add(new ProposalAttachmentDto
                {
                    Name = att.Name.Trim()[..Math.Min(att.Name.Trim().Length, 200)],
                    Url = url[..Math.Min(url.Length, 1000)],
                    StoredFileName = TrimOrNull(att.StoredFileName, 260),
                    ContentType = TrimOrNull(att.ContentType, 120),
                    SizeBytes = att.SizeBytes,
                });
            }
        }

        return null;
    }

    private static ProposalCandidate CandidateFromEntity(MentorTeachingProposal p)
        => new(
            ProposalId: p.Id,
            MentorId: p.MentorId,
            CertificateLevelId: p.CertificateLevelId,
            ProgramType: NormalizeProgramType(p.ProgramType),
            CoursePlanId: p.CoursePlanId,
            CourseObjective: TrimOrNull(p.CourseObjective, 1500),
            ExpectedStartDate: p.ExpectedStartDate,
            CourseDurationWeeks: p.CourseDurationWeeks,
            SessionDurationMinutes: p.SessionDurationMinutes,
            WeeklySessions: DeserializeSessions(p.WeeklySessionsJson),
            OptionalAttachments: DeserializeAttachments(p.OptionalAttachmentsJson));

    private static int ResolveHighestVerifiedRank(MentorProfile? profile, IEnumerable<string>? verifiedQualificationCodes)
    {
        var maxRank = 0;
        if (verifiedQualificationCodes is not null)
        {
            foreach (var code in verifiedQualificationCodes)
            {
                var r = MentorSlotPolicy.GetJlptRank(code);
                if (r > maxRank) maxRank = r;
            }
        }
        if (profile?.VerificationStatus == MentorVerificationStatus.Approved)
        {
            var profileRank = MentorSlotPolicy.GetJlptRank(profile.JlptLevel);
            if (profileRank > maxRank) maxRank = profileRank;
        }
        return maxRank;
    }

    private static string? FormatLevelBlockingReason(string? levelCode, LevelBlockInfo? block)
    {
        if (block is null) return null;
        var code = string.IsNullOrWhiteSpace(levelCode) ? string.Empty : $" {levelCode.Trim()}";
        return block.Status switch
        {
            "PendingReview" => $"Bạn đã có lớp{code} đang chờ duyệt.",
            "ChangesRequested" => $"Bạn đã có lớp{code} cần chỉnh sửa bổ sung.",
            "Approved" => $"Bạn đã có lớp{code} đã duyệt.",
            "RecruitingCohort" => $"Bạn đã có lớp{code} đang mở tuyển sinh.",
            "ActiveCohort" => $"Bạn đã có lớp{code} đang mở.",
            _ => block.Reason,
        };
    }

    private static DateOnly FindEarliestValidStartDate(DateOnly minDate, List<WeeklySessionSlotDto> weeklySessions)
    {
        if (weeklySessions.Count == 0) return minDate;
        var allowedWeekdays = weeklySessions.Select(s => s.Weekday).ToHashSet();
        for (var i = 0; i < 7; i++)
        {
            var candidate = minDate.AddDays(i);
            if (allowedWeekdays.Contains((int)candidate.DayOfWeek))
                return candidate;
        }
        return minDate;
    }

    private static List<GeneratedSessionOccurrenceResponse> ExpandOccurrences(
        DateOnly expectedStartDate,
        List<WeeklySessionSlotDto> weeklySessions,
        int durationWeeks,
        int sessionMinutes,
        DateTime nowUtc)
    {
        _ = nowUtc;
        if (weeklySessions.Count == 0 || durationWeeks <= 0) return [];
        var tuples = weeklySessions.Select(s => (s.Weekday, s.StartMinute)).ToList();
        var expanded = MentorSlotPolicy.ExpandRecurringSchedule(
            expectedStartDate,
            tuples,
            durationWeeks,
            sessionMinutes,
            BusinessZone.Value);

        return expanded.Select(occ => new GeneratedSessionOccurrenceResponse
        {
            SessionNumber = occ.SessionNumber,
            WeekNumber = occ.WeekNumber,
            Weekday = occ.Weekday,
            LocalDate = occ.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            LocalStart = Clock(occ.StartMinute),
            LocalEnd = Clock(occ.StartMinute + sessionMinutes),
            StartUtc = occ.StartUtc,
            EndUtc = occ.EndUtc,
        }).ToList();
    }

    private static List<WeeklySessionSlotDto> NormalizeWeeklySessions(List<WeeklySessionSlotDto>? raw, int sessionMinutes = 0)
    {
        if (raw is null || raw.Count == 0) return [];
        var effectiveMinutes = sessionMinutes > 0 ? sessionMinutes : MentorSlotPolicy.AppointmentDurationMinutes;
        return raw
            .Where(s => s is not null)
            .Select(s => new WeeklySessionSlotDto
            {
                Weekday = s.Weekday,
                StartMinute = s.StartMinute,
                EndMinute = s.EndMinute > 0 ? s.EndMinute : s.StartMinute + effectiveMinutes,
            })
            .OrderBy(s => (s.Weekday + 6) % 7)
            .ThenBy(s => s.StartMinute)
            .ToList();
    }

    private static List<WeeklySessionSlotDto> DeserializeSessions(string? json, int sessionMinutes = 0)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return NormalizeWeeklySessions(JsonSerializer.Deserialize<List<WeeklySessionSlotDto>>(json, JsonOpts), sessionMinutes);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<ProposalAttachmentDto> DeserializeAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ProposalAttachmentDto>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeProgramType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return MentorTeachingProgramType.StandardJlpt;
        var match = MentorTeachingProgramType.All.FirstOrDefault(x =>
            string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? MentorTeachingProgramType.StandardJlpt;
    }

    private static List<int> ParseAllowedSessionsPerWeek(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [2, 3, 4];
        var parsed = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n >= 1)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
        return parsed.Count > 0 ? parsed : [2, 3, 4];
    }

    private static string BuildSnapshotJson(MentorTeachingProposal p)
        => JsonSerializer.Serialize(new
        {
            p.CertificateLevelId,
            p.ProgramType,
            p.CoursePlanId,
            p.CourseTitle,
            p.ExpectedStartDate,
            p.CourseDurationWeeks,
            p.SessionDurationMinutes,
            WeeklySessions = DeserializeSessions(p.WeeklySessionsJson),
        }, JsonOpts);

    private static ProposalValidationIssue Error(string code, string message, int? weekday = null, int? startMinute = null)
        => new() { Code = code, Severity = "Error", Message = message, Weekday = weekday, StartMinute = startMinute };

    private static string? TrimOrNull(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen];
    }

    private static DateTime LocalTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(MentorTeachingConflictService.Utc(utc), BusinessZone.Value);

    private static DateOnly LocalWeekStart(DateOnly d)
        => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static DateTime WeekStartUtc(DateTime utc)
    {
        var monday = LocalWeekStart(DateOnly.FromDateTime(LocalTime(utc)));
        return LocalDateMinuteToUtc(monday, 0);
    }

    private static DateTime LocalDateMinuteToUtc(DateOnly localDate, int minuteOfDay)
    {
        var localDt = DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue).AddMinutes(minuteOfDay),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDt, BusinessZone.Value);
    }

    private static string Clock(int minuteOfDay)
        => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

    private static string WeekdayLabel(int weekday)
        => weekday == 0 ? "Chủ nhật" : $"Thứ {weekday + 1}";
}
