using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
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

    private sealed record ResolvedApprovalSchedule(
        bool CanApprove,
        string? BlockingReason,
        DateTime EnrollmentOpensAtUtc,
        DateTime EnrollmentClosesAtUtc,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        DateOnly? StartDate,
        DateOnly? EndDate,
        int TotalSessions,
        int SessionDurationMinutes,
        int TotalTeachingMinutes,
        int MaxLearners,
        decimal TuitionPerSession,
        decimal TotalCourseTuition,
        int PolicyVersion,
        bool WasShiftedDueToConflict,
        string? ConflictShiftReason,
        string? SchedulePreviewToken,
        List<GeneratedSessionOccurrenceResponse> Occurrences,
        List<ProposalValidationIssue> Issues,
        DateOnly? SuggestedEnrollmentDeadlineDate = null,
        string? SuggestedDeadlineReason = null,
        int MinimumPreparationDays = MentorSlotPolicy.MinimumPreparationDays);

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

        var availableLevels = activeLevelsRaw
            .Where(l => SupportedClassJlptCodes.Contains((l.Code ?? string.Empty).Trim()))
            .Select(l =>
            {
                var isQualified = mentorVerified
                    && (existingQualMap.ContainsKey(l.Id)
                        || MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, l.Code));
                string? qualificationReason = null;
                if (!mentorVerified || highestVerifiedRank <= 0)
                    qualificationReason = "Hồ sơ chuyên môn của Giáo viên chưa được xác minh.";
                else if (!isQualified)
                    qualificationReason = "Chưa được cấp quyền giảng dạy cấp độ này.";

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
                    IsAvailableForNewProposal = isQualified,
                    UnavailableReason = isQualified ? null : qualificationReason,
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
                SessionMinutes = p.SessionMinutes ?? policy!.SessionMinutes,
                TotalSessions = p.TotalSessions ?? p.SessionsPerWeek,
                Capacity = p.Capacity,
                Price = p.Price,
                IsPublished = p.IsPublished,
            })
            .ToListAsync(ct);

        var allProposals = await QueryProposalsWithIncludes()
            .Where(p => p.MentorId == claims.Id
                && p.Status != MentorTeachingProposalStatus.Withdrawn
                && !p.IsHiddenFromMentorHistory)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync(ct);

        // Mỗi thời điểm giữ tối đa 1 bản nháp Đang chọn có ít nhất 1 buổi/tuần
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

        var workload = await ComputeMentorWorkloadAsync(claims.Id, nowUtc, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(proposals.Select(p => p.Id).ToList(), ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);

        var mappedProposals = proposals
            .Select(p => MapProposal(p, policy!, qualCodes, linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false, isAdminView: false))
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

        var (policy, policyError) = await RequireEffectivePolicyAsync<List<TeachingProposalResponse>>(DateTime.UtcNow, ct);
        if (policyError is not null)
            return policyError;

        var query = QueryProposalsWithIncludes()
            .Where(p => p.MentorId == claims.Id
                && p.Status != MentorTeachingProposalStatus.Withdrawn
                && !p.IsHiddenFromMentorHistory);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(p => p.Status == normalizedStatus);
        }

        var rows = await query.OrderByDescending(p => p.UpdatedAtUtc).ToListAsync(ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(rows.Select(r => r.Id).ToList(), ct);

        return ApiResponse<List<TeachingProposalResponse>>.Success(
            rows.Select(p => MapProposal(p, policy!, qualCodes, linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false, isAdminView: false)).ToList());
    }

    public async Task<ApiResponse<TeachingProposalResponse>> GetMyProposalByIdAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể xem đề xuất này.", HttpStatusCode.Forbidden);

        var proposal = await QueryProposalsWithIncludes(includeEvents: true)
            .FirstOrDefaultAsync(p => p.Id == id
                && p.MentorId == claims.Id
                && p.Status != MentorTeachingProposalStatus.Withdrawn
                && !p.IsHiddenFromMentorHistory, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, policy!, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true, isAdminView: false);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<ProposalAttachmentDto>> GetAuthorizedAttachmentAsync(
        int proposalId, int attachmentIndex, bool isAdmin, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (isAdmin && claims.Role != Role.Admin)
            return ApiResponse<ProposalAttachmentDto>.Fail("Chỉ Quản trị viên mới có thể truy cập tài liệu này.", HttpStatusCode.Forbidden);
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

        var sanitizeErr = ValidateDraftFields(request, policy!, out var normalizedProgram, out var normalizedWeeks, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        var isCompletelyEmpty = normalizedSessions.Count == 0
            && !request.CertificateLevelId.HasValue
            && string.IsNullOrWhiteSpace(request.CourseTitle)
            && string.IsNullOrWhiteSpace(request.CourseObjective)
            && string.IsNullOrWhiteSpace(request.OptionalNotes)
            && normalizedAttachments.Count == 0;
        if (isCompletelyEmpty)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng chọn ít nhất một khung giờ hoặc nhập thông tin trước khi lưu.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_EMPTY_SELECTION");
        }

        if (normalizedSessions.Count == 0)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng chọn ít nhất một khung giờ.",
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

                var existingDrafts = await unitOfWork.MentorTeachingProposals.Query()
                    .Where(p => p.MentorId == claims.Id && p.Status == MentorTeachingProposalStatus.Draft)
                    .OrderByDescending(p => p.UpdatedAtUtc)
                    .ToListAsync(token);

                if (existingDrafts.Count > 0)
                {
                    var activeDraft = existingDrafts[0];
                    activeDraft.CertificateLevelId = request.CertificateLevelId;
                    activeDraft.ProgramType = normalizedProgram;
                    activeDraft.CoursePlanId = request.CoursePlanId;
                    activeDraft.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                    activeDraft.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                    activeDraft.ExpectedStartDate = null;
                    activeDraft.CourseDurationWeeks = normalizedWeeks;
                    activeDraft.SessionDurationMinutes = policy!.SessionMinutes;
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
                        unitOfWork.MentorTeachingProposals.Remove(existingDrafts[i]);

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
                    ExpectedStartDate = null,
                    CourseDurationWeeks = normalizedWeeks,
                    SessionDurationMinutes = policy!.SessionMinutes,
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
                "Không thể tạo đề xuất do có cập nhật đồng thời. Vui lòng thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == createdId, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, policy!, qualCodes, null, includeHistory: true, isAdminView: false));
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

        var sanitizeErr = ValidateDraftFields(request, policy!, out var normalizedProgram, out var normalizedWeeks, out var normalizedSessions, out var normalizedAttachments);
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
                        "Chỉ lịch đang chọn chưa gửi duyệt mới được cập nhật trực tiếp.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != proposal.Revision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Dữ liệu đang chọn vừa thay đổi. Vui lòng đồng bộ bản mới nhất trước khi tiếp tục.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                if (proposal.Status == MentorTeachingProposalStatus.Draft && normalizedSessions.Count == 0)
                {
                    unitOfWork.MentorTeachingProposals.Remove(proposal);
                    await unitOfWork.SaveChangesAsync(token);
                    deletedEmptyDraft = true;
                    return;
                }

                proposal.CertificateLevelId = request.CertificateLevelId;
                proposal.ProgramType = normalizedProgram;
                proposal.CoursePlanId = request.CoursePlanId;
                proposal.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                proposal.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                proposal.ExpectedStartDate = null;
                proposal.CourseDurationWeeks = request.CourseDurationWeeks.HasValue
                    ? normalizedWeeks
                    : (proposal.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks
                        ? proposal.CourseDurationWeeks
                        : MentorSlotPolicy.MinCourseDurationWeeks);
                proposal.SessionDurationMinutes = policy!.SessionMinutes;
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
                "Dữ liệu vừa thay đổi đồng thời. Vui lòng tải lại.",
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
                CourseDurationWeeks = normalizedWeeks,
                SessionDurationMinutes = policy!.SessionMinutes,
                MaxLearners = policy.MaxLearners,
                WeeklySessions = [],
            });
        }

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Success(MapProposal(loaded, policy!, qualCodes, null, includeHistory: true, isAdminView: false));
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
                    return;

                if (proposal.Status != MentorTeachingProposalStatus.Draft)
                {
                    failure = ApiResponse<bool>.Fail(
                        "Chỉ có thể xóa lịch đang chọn chưa gửi duyệt.",
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
            CourseDurationWeeks: request.CourseDurationWeeks ?? MentorSlotPolicy.MinCourseDurationWeeks,
            SessionDurationMinutes: activePolicy.SessionMinutes,
            WeeklySessions: NormalizeWeeklySessions(request.WeeklySessions, activePolicy.SessionMinutes),
            OptionalAttachments: [],
            ReferenceStartDate: null);

        var validation = await EvaluateProposalCandidateAsync(snapshot, activePolicy, nowUtc, ct);
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
                "Thiếu mã xác nhận thao tác gửi duyệt.",
                HttpStatusCode.BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
            return ApiResponse<TeachingProposalResponse>.Fail("Mã xác nhận gửi duyệt vượt quá độ dài cho phép.", HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_INVALID");

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
                        "Thao tác gửi duyệt đã được xử lý cho một đề xuất khác.",
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
                        "Dữ liệu đang chọn vừa thay đổi. Vui lòng đồng bộ bản mới nhất trước khi gửi duyệt.",
                        HttpStatusCode.Conflict,
                        "STALE_PROPOSAL_DATA");
                    return;
                }

                // Bảo toàn thời lượng buổi hiệu lực theo chính sách (mẫu tuần V4 không giữ ngày bắt đầu trước khi mở lớp)
                proposal.ExpectedStartDate = null;
                var candidate = CandidateFromEntity(proposal, policy!);
                var validation = await EvaluateProposalCandidateAsync(candidate, policy!, nowUtc, token);
                if (!validation.CanSubmit)
                {
                    failure = BuildSubmitValidationFailure(validation);
                    return;
                }

                proposal.SessionDurationMinutes = policy!.SessionMinutes;
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
                "Phát hiện cập nhật đồng thời khi gửi đề xuất. Vui lòng tải lại và thử lại.",
                HttpStatusCode.Conflict,
                "STALE_PROPOSAL_DATA");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã được gửi hoặc xảy ra cập nhật đồng thời. Vui lòng thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        return await BuildSubmittedProposalSuccessResponseAsync(resultId!.Value, claims.Id, policy!, nowUtc, ct);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ResubmitProposalAsync(
        int id, ResubmitTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể gửi lại đề xuất mở lớp.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Thiếu mã xác nhận thao tác gửi lại.",
                HttpStatusCode.BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
            return ApiResponse<TeachingProposalResponse>.Fail("Mã xác nhận gửi lại vượt quá độ dài cho phép.", HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_INVALID");

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
            CourseDurationWeeks = request.CourseDurationWeeks,
            SessionDurationMinutes = policy!.SessionMinutes,
            WeeklySessions = request.WeeklySessions,
            OptionalNotes = request.OptionalNotes,
            OptionalAttachments = request.OptionalAttachments,
            ExpectedRevision = request.ExpectedRevision,
        };

        var sanitizeErr = ValidateDraftFields(draftCheck, policy, out var normalizedProgram, out var normalizedWeeks, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        if (normalizedSessions.Count == 0)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng chọn ít nhất một khung giờ.",
                HttpStatusCode.UnprocessableEntity,
                "PROPOSAL_EMPTY_SCHEDULE");
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
                        "Thao tác gửi lại đã được sử dụng cho một đề xuất khác.",
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
                        "Chỉ đề xuất đang ở trạng thái Cần bổ sung mới có thể gửi lại xét duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_IMMUTABLE_STATUS");
                    return;
                }

                if (proposal.Revision != request.ExpectedRevision)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Đề xuất vừa thay đổi. Vui lòng đồng bộ bản mới nhất trước khi gửi lại.",
                        HttpStatusCode.Conflict,
                        "STALE_PROPOSAL_DATA");
                    return;
                }

                var resolvedLevelId = request.CertificateLevelId ?? proposal.CertificateLevelId;
                if (proposal.CertificateLevelId.HasValue && resolvedLevelId.HasValue && resolvedLevelId.Value != proposal.CertificateLevelId.Value)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Không thể thay đổi cấp độ JLPT của đề xuất đang cần bổ sung.",
                        HttpStatusCode.UnprocessableEntity,
                        "PROPOSAL_LEVEL_LOCKED_ON_REVISION");
                    return;
                }

                var resolvedWeeks = request.CourseDurationWeeks.HasValue
                    ? normalizedWeeks
                    : (proposal.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks
                        ? proposal.CourseDurationWeeks
                        : normalizedWeeks);
                var resolvedMinutes = policy.SessionMinutes;
                var candidate = new ProposalCandidate(
                    ProposalId: proposal.Id,
                    MentorId: claims.Id,
                    CertificateLevelId: resolvedLevelId,
                    ProgramType: normalizedProgram,
                    CoursePlanId: request.CoursePlanId,
                    CourseObjective: TrimOrNull(request.CourseObjective, 1500),
                    CourseDurationWeeks: resolvedWeeks,
                    SessionDurationMinutes: resolvedMinutes,
                    WeeklySessions: normalizedSessions,
                    OptionalAttachments: normalizedAttachments,
                    ReferenceStartDate: null);

                var validation = await EvaluateProposalCandidateAsync(candidate, policy, nowUtc, token);
                if (!validation.CanSubmit)
                {
                    failure = BuildSubmitValidationFailure(validation);
                    return;
                }

                var previousSessions = DeserializeSessions(proposal.WeeklySessionsJson, resolvedMinutes);
                var scheduleChanged = previousSessions.Count != normalizedSessions.Count
                    || proposal.CourseDurationWeeks != resolvedWeeks
                    || !previousSessions.SequenceEqual(normalizedSessions, WeeklySlotEqualityComparer.Instance);
                if (scheduleChanged)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        "Không hỗ trợ đổi khung giờ hoặc thời lượng đối với đề xuất đã gửi. Để đổi lịch dạy, vui lòng hủy đề xuất hiện tại và đăng ký đề xuất mới.",
                        HttpStatusCode.UnprocessableEntity,
                        "PROPOSAL_SCHEDULE_IMMUTABLE");
                    return;
                }

                proposal.CertificateLevelId = resolvedLevelId;
                proposal.ProgramType = normalizedProgram;
                proposal.CoursePlanId = request.CoursePlanId;
                proposal.CourseTitle = TrimOrNull(request.CourseTitle, 200);
                proposal.CourseObjective = TrimOrNull(request.CourseObjective, 1500);
                proposal.ExpectedStartDate = null;
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
                "Phát hiện cập nhật đồng thời khi gửi lại đề xuất. Vui lòng đồng bộ và thử lại.",
                HttpStatusCode.Conflict,
                "STALE_PROPOSAL_DATA");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã được gửi hoặc xảy ra cập nhật đồng thời. Vui lòng thử lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_CONFLICT_CONCURRENT_SUBMISSION");
        }

        if (failure is not null)
            return failure;

        return await BuildSubmittedProposalSuccessResponseAsync(resultId!.Value, claims.Id, policy!, nowUtc, ct);
    }

    private static ApiResponse<TeachingProposalResponse> BuildSubmitValidationFailure(TeachingProposalValidationResponse validation)
    {
        var blockingErrors = validation.Issues.Where(i => i.Severity == "Error").ToList();
        var chosen = blockingErrors.FirstOrDefault();
        var httpStatus = blockingErrors.Any(i => IsConflictCode(i.Code))
            ? HttpStatusCode.Conflict
            : HttpStatusCode.UnprocessableEntity;
        return ApiResponse<TeachingProposalResponse>.Fail(
            chosen?.Message ?? "Thông tin đề xuất mở lớp chưa hợp lệ.",
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
        var levelCode = proposal.CertificateLevelId.HasValue
            ? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Where(l => l.Id == proposal.CertificateLevelId.Value)
                .Select(l => l.Code)
                .FirstOrDefaultAsync(token)
            : null;

        if (string.IsNullOrWhiteSpace(proposal.CourseTitle))
        {
            proposal.CourseTitle = !string.IsNullOrWhiteSpace(levelCode)
                ? $"Lớp JLPT {levelCode} ({weeklySessionCount} buổi/tuần)"
                : $"Lớp JLPT ({weeklySessionCount} buổi/tuần)";
        }

        var fromStatus = proposal.Status;
        proposal.Status = MentorTeachingProposalStatus.PendingReview;
        proposal.SubmissionIdempotencyKey = idempotencyKey;
        proposal.SubmittedAtUtc = nowUtc;
        proposal.ReviewedAtUtc = null;
        proposal.ReviewedByAdminId = null;
        proposal.ReviewReason = null;
        proposal.ExpectedStartDate = null;
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
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(token);
        var mentorName = mentorUser is null
            ? "Giáo viên"
            : $"{mentorUser.FirstName} {mentorUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(mentorName)) mentorName = mentorUser?.Email ?? "Giáo viên";

        var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? "Đề xuất mở lớp" : proposal.CourseTitle;
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
            ? $"Giáo viên {mentorName} đã bổ sung thông tin{scheduleSuffix} và gửi lại đề xuất \"{titleLabel}\" ({weeklySessionCount} buổi/tuần × {proposal.CourseDurationWeeks} tuần) chờ duyệt."
            : $"Giáo viên {mentorName} vừa gửi đề xuất mở lớp \"{titleLabel}\" ({weeklySessionCount} buổi/tuần × {proposal.CourseDurationWeeks} tuần) chờ duyệt.";

        var notifications = adminIds.Select(adminId => new
        {
            UserId = adminId,
            Title = adminNotifTitle,
            Message = adminNotifMessage,
            Type = "MentorSchedule",
            LinkUrl = $"/admin/mentor-schedules/?proposalId={proposal.Id}",
        }).ToList();

        // Chỉ thông báo cho Quản trị viên phụ trách xét duyệt; không tạo thông báo tự gửi cho chính Giáo viên vừa thao tác.
        if (notifications.Count > 0)
        {
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
        var dto = MapProposal(loaded, policy, qualCodes, null, includeHistory: true, isAdminView: false);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(mentorId, nowUtc, ct);
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
                    return;

                if (proposal.Status == MentorTeachingProposalStatus.Withdrawn)
                {
                    removedSnapshot = proposal;
                    unitOfWork.MentorTeachingProposals.Remove(proposal);
                    await unitOfWork.SaveChangesAsync(token);
                    return;
                }

                var canWithdrawApproved = proposal.Status == MentorTeachingProposalStatus.Approved
                    && !await unitOfWork.StudyCohorts.Query().AsNoTracking()
                        .AnyAsync(c => c.SourceProposalId == proposal.Id, token);

                if (proposal.Status != MentorTeachingProposalStatus.PendingReview
                    && proposal.Status != MentorTeachingProposalStatus.ChangesRequested
                    && !canWithdrawApproved)
                {
                    failure = ApiResponse<TeachingProposalResponse>.Fail(
                        proposal.Status == MentorTeachingProposalStatus.OpenedClass
                            ? "Đề xuất này đã được mở lớp tuyển sinh; vui lòng thao tác trực tiếp trên lớp học tương ứng."
                            : "Chỉ có thể hủy đề xuất đang Chờ duyệt, Cần bổ sung hoặc Đã duyệt (chưa mở lớp).",
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
            ? MapProposal(removedSnapshot, policy!, qualCodes, null, includeHistory: false, isAdminView: false)
            : new TeachingProposalResponse
            {
                Id = id,
                MentorId = claims.Id,
                Status = MentorTeachingProposalStatus.Withdrawn,
            };
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<ApprovalSchedulePreviewResponse>> PreviewOpenEnrollmentAsync(
        int id, OpenEnrollmentPreviewRequest? request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<ApprovalSchedulePreviewResponse>.Fail(
                "Chỉ Giáo viên mới có thể xem trước lịch mở tuyển sinh.",
                HttpStatusCode.Forbidden,
                "FORBIDDEN_ROLE");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<ApprovalSchedulePreviewResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        var proposal = await QueryProposalsWithIncludes()
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, ct);
        if (proposal is null)
            return ApiResponse<ApprovalSchedulePreviewResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        var linkedCohortExists = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .AnyAsync(c => c.SourceProposalId == proposal.Id, ct);
        if (proposal.Status == MentorTeachingProposalStatus.OpenedClass || linkedCohortExists)
            return ApiResponse<ApprovalSchedulePreviewResponse>.Fail(
                "Đề xuất này đã được sử dụng để mở lớp tuyển sinh trước đó và không thể mở lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_ALREADY_OPENED_CLASS");

        if (proposal.Status != MentorTeachingProposalStatus.Approved)
            return ApiResponse<ApprovalSchedulePreviewResponse>.Fail(
                "Chỉ những đề xuất đã được Quản trị viên phê duyệt mới có thể mở tuyển sinh.",
                HttpStatusCode.Conflict,
                "PROPOSAL_NOT_APPROVED");

        var minEnrollmentDays = policy!.MinimumPublishedEnrollmentDays > 0
            ? policy.MinimumPublishedEnrollmentDays
            : MentorSlotPolicy.MinimumPublishedEnrollmentDays;
        var todayVn = DateOnly.FromDateTime(LocalTime(nowUtc));
        var minDeadlineDate = todayVn.AddDays(minEnrollmentDays);
        var requestedDeadline = request?.EnrollmentDeadlineLocalDate ?? minDeadlineDate;

        if (requestedDeadline < minDeadlineDate)
            return ApiResponse<ApprovalSchedulePreviewResponse>.Fail(
                $"Hạn tuyển sinh phải cách thời điểm hiện tại ít nhất {minEnrollmentDays} ngày (tối thiểu từ {minDeadlineDate:dd/MM/yyyy}).",
                HttpStatusCode.BadRequest,
                "ENROLLMENT_DEADLINE_TOO_EARLY");

        var resolved = await ResolveOpenEnrollmentScheduleAsync(proposal, requestedDeadline, policy, nowUtc, ct);
        var preview = ToOpenEnrollmentPreviewResponse(
            resolved,
            requestedDeadline,
            minDeadlineDate,
            DeserializeSessions(proposal.WeeklySessionsJson, resolved.SessionDurationMinutes).Count,
            Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, proposal.CourseDurationWeeks));

        return ApiResponse<ApprovalSchedulePreviewResponse>.Success(preview);
    }

    public async Task<ApiResponse<OpenEnrollmentConfirmResponse>> ConfirmOpenEnrollmentAsync(
        int id, ConfirmOpenEnrollmentRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                "Chỉ Giáo viên mới có thể mở lớp tuyển sinh từ đề xuất của mình.",
                HttpStatusCode.Forbidden,
                "FORBIDDEN_ROLE");

        if (request is null)
            return ApiResponse<OpenEnrollmentConfirmResponse>.Fail("Vui lòng chọn hạn tuyển sinh hợp lệ.");

        var idempotencyKey = TrimOrNull(request.IdempotencyKey, 128);
        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<OpenEnrollmentConfirmResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;

        var minEnrollmentDays = policy!.MinimumPublishedEnrollmentDays > 0
            ? policy.MinimumPublishedEnrollmentDays
            : MentorSlotPolicy.MinimumPublishedEnrollmentDays;
        var todayVn = DateOnly.FromDateTime(LocalTime(nowUtc));
        var minDeadlineDate = todayVn.AddDays(minEnrollmentDays);
        if (request.EnrollmentDeadlineLocalDate < minDeadlineDate)
            return ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                $"Hạn tuyển sinh phải cách thời điểm hiện tại ít nhất {minEnrollmentDays} ngày (tối thiểu từ {minDeadlineDate:dd/MM/yyyy}).",
                HttpStatusCode.BadRequest,
                "ENROLLMENT_DEADLINE_TOO_EARLY");

        var mentorUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == claims.Id)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        var mentorName = mentorUser is null
            ? "Giáo viên"
            : $"{mentorUser.FirstName} {mentorUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(mentorName)) mentorName = mentorUser?.Email ?? "Giáo viên";

        ApiResponse<OpenEnrollmentConfirmResponse>? failure = null;
        int? openedCohortId = null;
        string openedCohortName = string.Empty;
        string openedCohortStatus = "Published";
        DateTime openedAtUtc = nowUtc;
        DateTime closesAtUtc = nowUtc;
        DateTime startsAtUtc = nowUtc;
        DateTime endsAtUtc = nowUtc;
        int createdSessionsCount = 0;
        bool wasShifted = false;
        string? shiftReason = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .Include(p => p.CertificateLevel)
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);

                if (proposal is null)
                {
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                    return;
                }

                if (proposal.Status == MentorTeachingProposalStatus.OpenedClass)
                {
                    var existing = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                        .FirstOrDefaultAsync(c => c.SourceProposalId == proposal.Id, token);
                    if (!string.IsNullOrWhiteSpace(idempotencyKey)
                        && string.Equals(proposal.OpenEnrollmentIdempotencyKey, idempotencyKey, StringComparison.Ordinal)
                        && existing is not null)
                    {
                        openedCohortId = existing.Id;
                        openedCohortName = existing.Name;
                        openedCohortStatus = existing.Status;
                        openedAtUtc = existing.EnrollmentOpensAtUtc ?? existing.CreatedAtUtc;
                        closesAtUtc = existing.EnrollmentClosesAtUtc ?? existing.StartsAt;
                        startsAtUtc = existing.StartsAt;
                        endsAtUtc = existing.EndsAt;
                        return;
                    }

                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        "Đề xuất này đã được mở lớp tuyển sinh trước đó và chỉ được dùng để mở lớp một lần.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_ALREADY_OPENED_CLASS");
                    return;
                }

                var alreadyHasCohort = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                    .AnyAsync(c => c.SourceProposalId == proposal.Id, token);
                if (alreadyHasCohort)
                {
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        "Đề xuất này đã gắn với một lớp học trước đó và không thể mở lại.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_ALREADY_OPENED_CLASS");
                    return;
                }

                if (proposal.Status != MentorTeachingProposalStatus.Approved)
                {
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        "Chỉ có thể mở tuyển sinh từ đề xuất đã được phê duyệt.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_NOT_APPROVED");
                    return;
                }

                if (proposal.Revision != request.ExpectedRevision)
                {
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        "Đề xuất vừa được cập nhật. Vui lòng kiểm tra lại trước khi mở tuyển sinh.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                var resolved = await ResolveOpenEnrollmentScheduleAsync(
                    proposal,
                    request.EnrollmentDeadlineLocalDate,
                    policy,
                    nowUtc,
                    token);

                if (!resolved.CanApprove || resolved.Occurrences.Count == 0 || !resolved.StartDate.HasValue)
                {
                    var firstErr = resolved.Issues.FirstOrDefault(i => i.Severity == "Error");
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        resolved.BlockingReason ?? firstErr?.Message ?? "Không thể xếp lịch hợp lệ trong các tuần sắp tới.",
                        HttpStatusCode.Conflict,
                        firstErr?.Code ?? "OPEN_ENROLLMENT_SCHEDULE_CONFLICT",
                        errors: resolved.Issues.Where(i => i.Severity == "Error").Select(i => i.Message).ToList());
                    return;
                }

                if (!string.IsNullOrWhiteSpace(request.ExpectedSchedulePreviewToken)
                    && !string.Equals(request.ExpectedSchedulePreviewToken.Trim(), resolved.SchedulePreviewToken, StringComparison.Ordinal))
                {
                    failure = ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                        "Lịch học dự kiến, học phí hoặc sĩ số vừa thay đổi so với bản xem trước. Hệ thống đã tính lại — vui lòng kiểm tra và xác nhận lại.",
                        HttpStatusCode.Conflict,
                        "OPEN_ENROLLMENT_PREVIEW_STALE");
                    return;
                }

                var nextRevision = proposal.Revision + 1;
                var createdSlots = await CreateOfficialAvailabilitiesForOpenEnrollmentAsync(
                    proposal, policy, resolved, nextRevision, nowUtc, mentorName, token);
                var createdCohort = await CreateCohortForOpenEnrollmentAsync(
                    proposal, createdSlots, policy, nowUtc, resolved.EnrollmentClosesAtUtc, token);

                proposal.Status = MentorTeachingProposalStatus.OpenedClass;
                proposal.OpenedClassAtUtc = nowUtc;
                proposal.OpenEnrollmentIdempotencyKey = idempotencyKey;
                proposal.ExpectedStartDate = resolved.StartDate;
                proposal.SessionDurationMinutes = resolved.SessionDurationMinutes;
                proposal.GroupPolicyId = policy.Id;
                proposal.PolicyVersionSnapshot = policy.Version;
                proposal.Revision = nextRevision;
                proposal.UpdatedAtUtc = nowUtc;
                unitOfWork.MentorTeachingProposals.Update(proposal);

                await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
                {
                    ProposalId = proposal.Id,
                    ActorUserId = claims.Id,
                    ActorRole = Role.Mentor.ToString(),
                    Action = "OpenedClass",
                    FromStatus = MentorTeachingProposalStatus.Approved,
                    ToStatus = MentorTeachingProposalStatus.OpenedClass,
                    RevisionAfter = proposal.Revision,
                    Reason = $"Mở tuyển sinh đến hết {request.EnrollmentDeadlineLocalDate:dd/MM/yyyy}",
                    SnapshotJson = JsonSerializer.Serialize(new
                    {
                        GeneratedAvailabilityIds = createdSlots.Select(s => s.Id).ToList(),
                        LinkedCohortId = createdCohort.Id,
                        LinkedCohortName = createdCohort.Name,
                        EnrollmentDeadlineLocalDate = request.EnrollmentDeadlineLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        EnrollmentOpensAtUtc = nowUtc,
                        EnrollmentClosesAtUtc = createdCohort.EnrollmentClosesAtUtc,
                        StartsAtUtc = createdCohort.StartsAt,
                        EndsAtUtc = createdCohort.EndsAt,
                        TotalTuitionSnapshot = createdCohort.TotalTuitionSnapshot,
                    }, JsonOpts),
                    CreatedAtUtc = nowUtc,
                }, token);

                var levelCode = proposal.CertificateLevel?.Code ?? "JLPT";
                var vnStart = LocalTime(createdCohort.StartsAt);
                unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
                {
                    EventKey = $"proposal-opened-class:{proposal.Id}:c{createdCohort.Id}",
                    AggregateType = "StudyCohort",
                    AggregateId = createdCohort.Id.ToString(CultureInfo.InvariantCulture),
                    EventType = "CohortLearningNotification",
                    EventVersion = 1,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        Notifications = new[]
                        {
                            new
                            {
                                UserId = proposal.MentorId,
                                Title = $"Lớp {levelCode} đã mở tuyển sinh",
                                Message = $"Lớp \"{createdCohort.Name}\" ({createdSlots.Count} buổi) đã mở tuyển sinh đến hết {request.EnrollmentDeadlineLocalDate:dd/MM/yyyy} và dự kiến bắt đầu ngày {vnStart:dd/MM/yyyy}.",
                                Type = "CohortLifecycle",
                                LinkUrl = $"/mentor-study-courses/{createdCohort.Id}/",
                            },
                        },
                    }),
                    CreatedAtUtc = nowUtc,
                    AvailableAtUtc = nowUtc,
                });

                await unitOfWork.SaveChangesAsync(token);

                openedCohortId = createdCohort.Id;
                openedCohortName = createdCohort.Name;
                openedCohortStatus = createdCohort.Status;
                openedAtUtc = createdCohort.EnrollmentOpensAtUtc ?? nowUtc;
                closesAtUtc = createdCohort.EnrollmentClosesAtUtc ?? resolved.EnrollmentClosesAtUtc;
                startsAtUtc = createdCohort.StartsAt;
                endsAtUtc = createdCohort.EndsAt;
                createdSessionsCount = createdSlots.Count;
                wasShifted = resolved.WasShiftedDueToConflict;
                shiftReason = resolved.ConflictShiftReason;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                "Đề xuất vừa thay đổi đồng thời. Vui lòng tải lại trước khi mở tuyển sinh.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<OpenEnrollmentConfirmResponse>.Fail(
                "Đề xuất đã được mở lớp hoặc lịch vừa phát sinh xung đột. Vui lòng tải lại.",
                HttpStatusCode.Conflict,
                "OPEN_ENROLLMENT_CONCURRENT_CONFLICT");
        }

        if (failure is not null)
            return failure;

        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyUsersAsync([claims.Id], "proposal_opened_class", ct);
        }

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(loaded.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([loaded.Id], ct);
        var dto = MapProposal(loaded, policy, qualCodes, linkedCohorts.GetValueOrDefault(loaded.Id), includeHistory: true, isAdminView: false);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(loaded.MentorId, nowUtc, ct);

        return ApiResponse<OpenEnrollmentConfirmResponse>.Success(new OpenEnrollmentConfirmResponse
        {
            Proposal = dto,
            CohortId = openedCohortId ?? (dto.LinkedCohortId ?? 0),
            CohortName = openedCohortName,
            CohortStatus = openedCohortStatus,
            EnrollmentOpensAtUtc = openedAtUtc,
            EnrollmentClosesAtUtc = closesAtUtc,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            CreatedSessionsCount = createdSessionsCount > 0 ? createdSessionsCount : dto.OccurrencesPreview.Count,
            WasShiftedDueToConflict = wasShifted,
            ConflictShiftReason = shiftReason,
        });
    }

    public async Task<ApiResponse<bool>> HideRejectedProposalAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<bool>.Fail("Chỉ Giáo viên mới có thể xóa đề xuất bị từ chối khỏi lịch sử cá nhân.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        ApiResponse<bool>? failure = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var proposal = await unitOfWork.MentorTeachingProposals.Query()
                    .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id && p.Status != MentorTeachingProposalStatus.Withdrawn, token);
                if (proposal is null)
                    return;

                if (proposal.Status != MentorTeachingProposalStatus.Rejected)
                {
                    failure = ApiResponse<bool>.Fail(
                        "Chỉ có thể xóa khỏi lịch sử cá nhân các đề xuất đã bị từ chối.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_HIDE_ONLY_REJECTED");
                    return;
                }

                if (proposal.IsHiddenFromMentorHistory)
                    return;

                proposal.IsHiddenFromMentorHistory = true;
                proposal.HiddenFromMentorAtUtc = nowUtc;
                proposal.UpdatedAtUtc = nowUtc;
                proposal.Revision++;

                await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
                {
                    ProposalId = proposal.Id,
                    ActorUserId = claims.Id,
                    ActorRole = claims.Role.ToString(),
                    Action = "HiddenByMentor",
                    FromStatus = proposal.Status,
                    ToStatus = proposal.Status,
                    RevisionAfter = proposal.Revision,
                    Reason = "Giáo viên xóa đề xuất bị từ chối khỏi lịch sử cá nhân.",
                    CreatedAtUtc = nowUtc,
                }, token);

                await unitOfWork.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<bool>.Fail(
                "Đề xuất vừa thay đổi đồng thời. Vui lòng tải lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }

        return failure ?? ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ReopenForRevisionAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể tạo lại từ đề xuất này.", HttpStatusCode.Forbidden);

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
                    "Chỉ có thể tạo lại từ đề xuất đã bị từ chối hoặc đã hủy.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_IMMUTABLE_STATUS");
                return;
            }

            var clonedWeeks = source.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks
                ? source.CourseDurationWeeks
                : MentorSlotPolicy.MinCourseDurationWeeks;

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
                activeDraft.ExpectedStartDate = null;
                activeDraft.CourseDurationWeeks = clonedWeeks;
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
                    Reason = "Tạo bản đăng ký mới từ đề xuất trước đó.",
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
                ExpectedStartDate = null,
                CourseDurationWeeks = clonedWeeks,
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
                Reason = "Tạo bản đăng ký mới từ đề xuất trước đó.",
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
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, effectivePolicy, qualCodes, null, includeHistory: true, isAdminView: false));
    }

    // ───────────────────────────── ADMIN ENDPOINTS ─────────────────────────────

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetAdminProposalsAsync(
        string? status = null, int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ Quản trị viên mới có quyền xem hàng đợi đề xuất mở lớp.", HttpStatusCode.Forbidden);

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
            var dto = MapProposal(row, policy!, qualsByMentor.GetValueOrDefault(row.MentorId) ?? [], linkedCohorts.GetValueOrDefault(row.Id), includeHistory: false, isAdminView: true);
            if (!workloadCache.TryGetValue(row.MentorId, out var workload))
            {
                workload = await ComputeMentorWorkloadAsync(row.MentorId, nowUtc, ct);
                workloadCache[row.MentorId] = workload;
            }
            dto.MentorWorkload = workload;
            result.Add(dto);
        }

        return ApiResponse<List<TeachingProposalResponse>>.Success(result);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> GetAdminProposalByIdAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Quản trị viên mới có quyền xem chi tiết đề xuất này.", HttpStatusCode.Forbidden);

        var proposal = await QueryProposalsWithIncludes(includeEvents: true)
            .FirstOrDefaultAsync(p => p.Id == id && p.Status != MentorTeachingProposalStatus.Withdrawn, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<TeachingProposalResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(proposal.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, policy!, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true, isAdminView: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(proposal.MentorId, nowUtc, ct);

        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ReviewProposalAsync(
        int id, ReviewTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Quản trị viên mới có quyền phê duyệt đề xuất mở lớp.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.Decision))
            return ApiResponse<TeachingProposalResponse>.Fail("Vui lòng chọn quyết định xử lý hợp lệ.");

        var decision = request.Decision.Trim();
        var isApprove = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isRequestChanges = string.Equals(decision, "RequestChanges", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isRequestChanges && !isReject)
            return ApiResponse<TeachingProposalResponse>.Fail("Quyết định xét duyệt không hợp lệ.", HttpStatusCode.BadRequest, "INVALID_REVIEW_DECISION");

        var trimmedReason = TrimOrNull(request.Reason, 1500);
        if ((isRequestChanges || isReject) && string.IsNullOrWhiteSpace(trimmedReason))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng nhập lý do rõ ràng khi từ chối hoặc yêu cầu Giáo viên bổ sung thông tin.",
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
            ? "Quản trị viên"
            : $"{adminUser.FirstName} {adminUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(adminName)) adminName = adminUser?.Email ?? "Quản trị viên";

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
                        "Đề xuất vừa được cập nhật. Vui lòng tải lại trước khi quyết định.",
                        HttpStatusCode.Conflict,
                        "PROPOSAL_REVISION_CONFLICT");
                    return;
                }

                var fromStatus = proposal.Status;

                if (isApprove)
                {
                    var candidate = CandidateFromEntity(proposal, policy!);
                    var (_, structureIssues) = await EvaluateProposalStructureAsync(candidate, policy!, token);
                    var firstErr = structureIssues.FirstOrDefault(i => i.Severity == "Error");
                    if (firstErr is not null)
                    {
                        failure = ApiResponse<TeachingProposalResponse>.Fail(
                            firstErr.Message,
                            HttpStatusCode.Conflict,
                            firstErr.Code,
                            errors: structureIssues.Where(i => i.Severity == "Error").Select(i => i.Message).ToList());
                        return;
                    }

                    var nextRevision = proposal.Revision + 1;
                    proposal.ExpectedStartDate = null;
                    proposal.SessionDurationMinutes = policy!.SessionMinutes;
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

                    if (proposal.CertificateLevelId.HasValue && unitOfWork.MentorTeachingQualifications != null)
                    {
                        var existingQual = await unitOfWork.MentorTeachingQualifications.Query()
                            .FirstOrDefaultAsync(q =>
                                q.MentorUserId == proposal.MentorId
                                && q.CertificateLevelId == proposal.CertificateLevelId.Value, token);
                        if (existingQual is null)
                        {
                            await unitOfWork.MentorTeachingQualifications.AddAsync(new MentorTeachingQualification
                            {
                                MentorUserId = proposal.MentorId,
                                CertificateLevelId = proposal.CertificateLevelId.Value,
                                IsActive = true,
                                VerifiedByAdminId = claims.Id,
                                VerifiedAtUtc = nowUtc,
                            }, token);
                        }
                    }
                }
                else if (isRequestChanges)
                {
                    proposal.Status = MentorTeachingProposalStatus.ChangesRequested;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.SubmissionIdempotencyKey = null;
                    proposal.ExpectedStartDate = null;
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
                    proposal.ExpectedStartDate = null;
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
                    SnapshotJson = isApprove
                        ? JsonSerializer.Serialize(new
                        {
                            PolicyVersion = policy!.Version,
                            SessionDurationMinutes = proposal.SessionDurationMinutes,
                            CourseDurationWeeks = proposal.CourseDurationWeeks,
                            DecoupledApproval = true,
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
                        PolicyVersion = policy?.Version,
                    }),
                    Reason = trimmedReason,
                    CreatedAtUtc = nowUtc,
                }, token);

                var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? "Đề xuất mở lớp" : proposal.CourseTitle;
                var levelCode = proposal.CertificateLevelId.HasValue
                    ? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                        .Where(l => l.Id == proposal.CertificateLevelId.Value)
                        .Select(l => l.Code)
                        .FirstOrDefaultAsync(token)
                    : null;
                var levelLabel = string.IsNullOrWhiteSpace(levelCode) ? "JLPT" : $"JLPT {levelCode}";
                var notifTitle = isApprove
                    ? $"Đề xuất mở lớp {levelLabel} đã được phê duyệt"
                    : isRequestChanges
                        ? $"Đề xuất mở lớp {levelLabel} cần bổ sung"
                        : $"Đề xuất mở lớp {levelLabel} chưa được chấp thuận";
                var notifMessage = isApprove
                    ? $"Đề xuất \"{titleLabel}\" đã được phê duyệt. Bạn có thể chọn hạn tuyển sinh và chủ động mở tuyển sinh tại trang Danh sách đăng ký."
                    : isRequestChanges
                        ? $"Quản trị viên yêu cầu bạn bổ sung thông tin cho đề xuất \"{titleLabel}\": {trimmedReason}"
                        : $"Đề xuất mở lớp \"{titleLabel}\" chưa được chấp thuận. Lý do: {trimmedReason}";
                var notifLinkUrl = $"/mentor-proposal-history/?proposalId={proposal.Id}";

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
        var dto = MapProposal(loaded, policy!, qualCodes, linkedCohorts.GetValueOrDefault(loaded.Id), includeHistory: true, isAdminView: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(loaded.MentorId, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<BulkApproveTeachingProposalsResponse>> PrevalidateBulkApproveAsync(
        BulkApproveTeachingProposalsRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Chỉ Quản trị viên mới có quyền kiểm tra phê duyệt hàng loạt đề xuất mở lớp.",
                HttpStatusCode.Forbidden);

        var (normalizedItems, inputError) = NormalizeBulkApproveItems(request);
        if (inputError is not null)
            return inputError;

        var nowUtc = DateTime.UtcNow;
        var (policy, policyError) = await RequireEffectivePolicyAsync<BulkApproveTeachingProposalsResponse>(nowUtc, ct);
        if (policyError is not null)
            return policyError;
        var evaluation = await EvaluateBulkApproveBatchAsync(normalizedItems, policy!, nowUtc, requireExpectedRevision: false, checkPreviewTokens: false, ct);
        return ApiResponse<BulkApproveTeachingProposalsResponse>.Success(evaluation.Response);
    }

    public async Task<ApiResponse<BulkApproveTeachingProposalsResponse>> BulkApproveProposalsAsync(
        BulkApproveTeachingProposalsRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Chỉ Quản trị viên mới có quyền phê duyệt hàng loạt đề xuất mở lớp.",
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
            ? "Quản trị viên"
            : $"{adminUser.FirstName} {adminUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(adminName)) adminName = adminUser?.Email ?? "Quản trị viên";

        ApiResponse<BulkApproveTeachingProposalsResponse>? failure = null;
        BulkApproveTeachingProposalsResponse? committedResponse = null;
        var approvedMentorIds = new List<int>();
        var batchId = Guid.NewGuid().ToString("N")[..12];

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var requestedIds = normalizedItems.Select(x => x.ProposalId).ToList();

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

                var evaluation = await EvaluateBulkApproveBatchAsync(
                    normalizedItems,
                    policy!,
                    nowUtc,
                    requireExpectedRevision: true,
                    checkPreviewTokens: true,
                    token);

                if (!evaluation.Response.AllEligible)
                {
                    var invalidItems = evaluation.Response.Results.Where(r => !r.IsEligible).ToList();
                    var firstIssue = invalidItems.SelectMany(r => r.Issues).FirstOrDefault(i => i.Severity == "Error");
                    var errorSummary = invalidItems
                        .SelectMany(r => r.Issues.Where(i => i.Severity == "Error").Take(2)
                            .Select(i => $"{(string.IsNullOrWhiteSpace(r.CourseTitle) ? "Đề xuất mở lớp" : r.CourseTitle)} ({(string.IsNullOrWhiteSpace(r.MentorName) ? "Giáo viên" : r.MentorName)}): {i.Message}"))
                        .Take(10)
                        .ToList();

                    failure = ApiResponse<BulkApproveTeachingProposalsResponse>.FailWithResult(
                        evaluation.Response,
                        $"Không thể phê duyệt hàng loạt vì có {invalidItems.Count}/{evaluation.Response.TotalRequested} đề xuất chưa đủ điều kiện hoặc lịch học đã thay đổi.",
                        HttpStatusCode.Conflict,
                        firstIssue?.Code ?? "PROPOSAL_BULK_VALIDATION_FAILED",
                        errorSummary);
                    return;
                }

                var trackedProposals = await unitOfWork.MentorTeachingProposals.Query()
                    .Where(p => requestedIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, token);

                var pendingRecords = new List<(MentorTeachingProposal Proposal, int OldRevision, BulkApproveProposalItemResult ItemResult)>(normalizedItems.Count);

                foreach (var item in normalizedItems)
                {
                    if (!trackedProposals.TryGetValue(item.ProposalId, out var proposal)
                        || proposal.Status != MentorTeachingProposalStatus.PendingReview
                        || (item.ExpectedRevision.HasValue && proposal.Revision != item.ExpectedRevision.Value)
                        || !evaluation.SchedulesByProposalId.TryGetValue(proposal.Id, out var resolved)
                        || !resolved.CanApprove)
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
                    proposal.ExpectedStartDate = null;
                    proposal.SessionDurationMinutes = resolved.SessionDurationMinutes;
                    proposal.Status = MentorTeachingProposalStatus.Approved;
                    proposal.ReviewedAtUtc = nowUtc;
                    proposal.ReviewedByAdminId = claims.Id;
                    proposal.ReviewReason = trimmedReason;
                    proposal.GroupPolicyId = policy!.Id;
                    proposal.PolicyVersionSnapshot = policy.Version;
                    proposal.Revision = nextRevision;
                    proposal.UpdatedAtUtc = nowUtc;
                    unitOfWork.MentorTeachingProposals.Update(proposal);

                    var itemResult = evaluation.Response.Results.First(r => r.ProposalId == proposal.Id);
                    pendingRecords.Add((proposal, oldRevision, itemResult));
                }

                await unitOfWork.SaveChangesAsync(token);

                foreach (var (proposal, oldRevision, itemResult) in pendingRecords)
                {
                    if (proposal.CertificateLevelId.HasValue && unitOfWork.MentorTeachingQualifications != null)
                    {
                        var existingQual = await unitOfWork.MentorTeachingQualifications.Query()
                            .FirstOrDefaultAsync(q =>
                                q.MentorUserId == proposal.MentorId
                                && q.CertificateLevelId == proposal.CertificateLevelId.Value, token);
                        if (existingQual is null)
                        {
                            await unitOfWork.MentorTeachingQualifications.AddAsync(new MentorTeachingQualification
                            {
                                MentorUserId = proposal.MentorId,
                                CertificateLevelId = proposal.CertificateLevelId.Value,
                                IsActive = true,
                                VerifiedByAdminId = claims.Id,
                                VerifiedAtUtc = nowUtc,
                            }, token);
                        }
                    }

                    itemResult.Status = MentorTeachingProposalStatus.Approved;
                    itemResult.CurrentRevision = proposal.Revision;
                    itemResult.Approved = true;

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
                            PolicyVersion = policy!.Version,
                            SessionDurationMinutes = proposal.SessionDurationMinutes,
                            CourseDurationWeeks = proposal.CourseDurationWeeks,
                            DecoupledApproval = true,
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
                            PolicyVersion = policy.Version,
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
                    var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất {bulkLevelLabel}" : proposal.CourseTitle;

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
                                    Title = $"Đề xuất mở lớp {bulkLevelLabel} đã được phê duyệt",
                                    Message = $"Đề xuất \"{titleLabel}\" đã được phê duyệt. Bạn có thể chọn hạn tuyển sinh và chủ động mở tuyển sinh tại trang Danh sách đăng ký.",
                                    Type = "MentorSchedule",
                                    LinkUrl = $"/mentor-proposal-history/?proposalId={proposal.Id}",
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
                "Một hoặc nhiều đề xuất vừa được xử lý đồng thời. Không có đề xuất nào được phê duyệt — vui lòng tải lại.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }
        catch (DbUpdateException ex) when (IsPgConflict(ex))
        {
            return ApiResponse<BulkApproveTeachingProposalsResponse>.Fail(
                "Xung đột dữ liệu đồng thời khi phê duyệt hàng loạt. Vui lòng tải lại và thử lại.",
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

    private async Task<List<MentorAvailability>> CreateOfficialAvailabilitiesForOpenEnrollmentAsync(
        MentorTeachingProposal proposal,
        MentorGroupPolicy policy,
        ResolvedApprovalSchedule resolved,
        int nextRevision,
        DateTime nowUtc,
        string mentorName,
        CancellationToken token)
    {
        var createdSlots = new List<MentorAvailability>(resolved.Occurrences.Count);
        foreach (var occ in resolved.Occurrences)
        {
            var localDate = DateOnly.ParseExact(occ.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var slot = new MentorAvailability
            {
                MentorId = proposal.MentorId,
                TeachingProposalId = proposal.Id,
                GenerationKey = $"v4-open:p{proposal.Id}:v{nextRevision}:s{occ.SessionNumber}",
                PolicyVersion = policy.Version,
                GroupPolicyId = policy.Id,
                SessionMinutesSnapshot = resolved.SessionDurationMinutes,
                RestMinutesSnapshot = policy.RestMinutes,
                LocalBusinessDate = localDate,
                MaxLearners = resolved.MaxLearners,
                BookedCount = 0,
                StartTime = DateTime.SpecifyKind(occ.StartUtc, DateTimeKind.Unspecified),
                EndTime = DateTime.SpecifyKind(occ.EndUtc, DateTimeKind.Unspecified),
                RoomId = Guid.NewGuid(),
                AttendanceTrackingEnabled = true,
                IsApproved = true,
                PublishedAt = nowUtc,
                AdminName = mentorName,
                IsDeleted = false,
                CreatedDate = nowUtc,
                ModifiedDate = nowUtc,
            };
            createdSlots.Add(slot);
            await unitOfWork.MentorAvailabilities.AddAsync(slot, token);
        }
        return createdSlots;
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
                    "Danh sách phê duyệt chứa đề xuất không hợp lệ.",
                    HttpStatusCode.BadRequest,
                    "PROPOSAL_ID_INVALID"));
            }

            if (seen.Add(item.ProposalId))
                normalized.Add(item);
        }

        return (normalized, null);
    }

    private sealed record BulkApproveEvaluationResult(
        BulkApproveTeachingProposalsResponse Response,
        Dictionary<int, ResolvedApprovalSchedule> SchedulesByProposalId);

    private async Task<BulkApproveEvaluationResult> EvaluateBulkApproveBatchAsync(
        List<BulkApproveProposalItemRequest> items,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        bool requireExpectedRevision,
        bool checkPreviewTokens,
        CancellationToken ct)
    {
        var requestedIds = items.Select(i => i.ProposalId).ToList();
        var loadedProposals = await QueryProposalsWithIncludes(includeEvents: false)
            .Where(p => requestedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var orderedResults = new List<BulkApproveProposalItemResult>(items.Count);
        var schedulesById = new Dictionary<int, ResolvedApprovalSchedule>();

        foreach (var reqItem in items)
        {
            if (!loadedProposals.TryGetValue(reqItem.ProposalId, out var proposal))
            {
                orderedResults.Add(new BulkApproveProposalItemResult
                {
                    ProposalId = reqItem.ProposalId,
                    Status = "NotFound",
                    IsEligible = false,
                    Issues = [Error("PROPOSAL_NOT_FOUND", "Không tìm thấy đề xuất mở lớp.")],
                });
                continue;
            }

            var mentorName = proposal.Mentor is null
                ? "Giáo viên"
                : $"{proposal.Mentor.FirstName} {proposal.Mentor.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(mentorName)) mentorName = proposal.Mentor?.Email ?? "Giáo viên";

            var weeklySessions = DeserializeSessions(proposal.WeeklySessionsJson, policy.SessionMinutes);
            var durationWeeks = Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, proposal.CourseDurationWeeks);
            var totalSessions = weeklySessions.Count * durationWeeks;
            var tuitionPerSession = policy.GetTuitionPerSession(proposal.CertificateLevel?.Code);

            var itemResult = new BulkApproveProposalItemResult
            {
                ProposalId = proposal.Id,
                MentorId = proposal.MentorId,
                MentorName = mentorName,
                JlptLevelCode = proposal.CertificateLevel?.Code,
                CourseTitle = proposal.CourseTitle,
                CourseDurationWeeks = durationWeeks,
                WeeklySessionCount = weeklySessions.Count,
                TotalSessions = totalSessions,
                MaxLearners = policy.MaxLearners,
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
                itemResult.IsEligible = false;
                orderedResults.Add(itemResult);
                continue;
            }

            if (requireExpectedRevision && (!reqItem.ExpectedRevision.HasValue || proposal.Revision != reqItem.ExpectedRevision.Value))
            {
                itemResult.Issues.Add(Error(
                    "PROPOSAL_REVISION_CONFLICT",
                    "Đề xuất vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trước khi phê duyệt."));
            }

            var (_, structureIssues) = await EvaluateProposalStructureAsync(
                CandidateFromEntity(proposal, policy),
                policy,
                ct);

            foreach (var err in structureIssues.Where(i => i.Severity == "Error"))
            {
                itemResult.Issues.Add(err);
            }

            itemResult.ExpectedStartDate = null;
            itemResult.ProjectedEndDate = null;
            itemResult.EnrollmentOpensAtUtc = null;
            itemResult.EnrollmentClosesAtUtc = null;
            itemResult.TotalSessions = totalSessions;
            itemResult.MaxLearners = policy.MaxLearners;
            itemResult.TuitionPerSession = tuitionPerSession;
            itemResult.TotalCourseTuition = tuitionPerSession * totalSessions;
            itemResult.TuitionPolicyVersion = tuitionPerSession > 0 ? policy.Version : null;
            itemResult.WasShiftedDueToConflict = false;
            itemResult.ConflictShiftReason = null;
            itemResult.SchedulePreviewToken = null;

            itemResult.IsEligible = !itemResult.Issues.Any(i => i.Severity == "Error");
            if (itemResult.IsEligible)
            {
                schedulesById[proposal.Id] = new ResolvedApprovalSchedule(
                    CanApprove: true,
                    BlockingReason: null,
                    EnrollmentOpensAtUtc: nowUtc,
                    EnrollmentClosesAtUtc: nowUtc,
                    StartsAtUtc: nowUtc,
                    EndsAtUtc: nowUtc,
                    StartDate: null,
                    EndDate: null,
                    TotalSessions: totalSessions,
                    SessionDurationMinutes: policy.SessionMinutes,
                    TotalTeachingMinutes: totalSessions * policy.SessionMinutes,
                    MaxLearners: policy.MaxLearners,
                    TuitionPerSession: tuitionPerSession,
                    TotalCourseTuition: tuitionPerSession * totalSessions,
                    PolicyVersion: policy.Version,
                    WasShiftedDueToConflict: false,
                    ConflictShiftReason: null,
                    SchedulePreviewToken: null,
                    Occurrences: [],
                    Issues: structureIssues);
            }

            orderedResults.Add(itemResult);
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
            schedulesById);
    }

    public async Task<ApiResponse<RepairInvalidProposalSchedulesResponse>> RepairInvalidNeedsRevisionSchedulesAsync(CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<RepairInvalidProposalSchedulesResponse>.Fail(
                "Chỉ Quản trị viên mới có quyền kiểm tra lịch đề xuất cần bổ sung.",
                HttpStatusCode.Forbidden);

        var (policy, policyError) = await RequireEffectivePolicyAsync<RepairInvalidProposalSchedulesResponse>(DateTime.UtcNow, ct);
        if (policyError is not null)
            return policyError;

        var candidates = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Where(p => p.Status == MentorTeachingProposalStatus.ChangesRequested)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var response = new RepairInvalidProposalSchedulesResponse
        {
            ScannedCount = candidates.Count,
        };

        foreach (var candidate in candidates)
        {
            var sessions = DeserializeSessions(candidate.WeeklySessionsJson, policy!.SessionMinutes);
            if (sessions.Count >= 1 && candidate.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks)
                continue;

            response.InvalidFoundCount++;
            response.SkippedCount++;
            response.Items.Add(new RepairedProposalScheduleItem
            {
                ProposalId = candidate.Id,
                CourseTitle = candidate.CourseTitle,
                JlptLevelCode = candidate.CertificateLevel?.Code,
                Action = "Skipped",
                PreviousWeeklyCount = sessions.Count,
                RestoredWeeklyCount = sessions.Count,
                Reason = "Đề xuất cần Giáo viên bổ sung ít nhất 1 khung giờ hàng tuần hợp lệ và số tuần giảng dạy dương.",
            });
        }

        return ApiResponse<RepairInvalidProposalSchedulesResponse>.Success(response);
    }

    private async Task<StudyCohort> CreateCohortForOpenEnrollmentAsync(
        MentorTeachingProposal proposal,
        List<MentorAvailability> createdSlots,
        MentorGroupPolicy policy,
        DateTime enrollmentOpensAtUtc,
        DateTime enrollmentClosesAtUtc,
        CancellationToken token)
    {
        var orderedSlots = createdSlots.OrderBy(s => s.StartTime).ToList();
        if (orderedSlots.Count == 0)
            throw new InvalidOperationException("Không thể tạo lớp học khi chưa có buổi học hợp lệ.");

        var levelId = proposal.CertificateLevelId
            ?? throw new InvalidOperationException("Đề xuất mở lớp thiếu trình độ JLPT.");
        var levelCode = proposal.CertificateLevel?.Code
            ?? await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .Where(l => l.Id == levelId)
                .Select(l => l.Code)
                .FirstOrDefaultAsync(token)
            ?? "JLPT";

        var weeklySessions = Math.Max(1, DeserializeSessions(proposal.WeeklySessionsJson).Count);
        var durationWeeks = Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, proposal.CourseDurationWeeks);
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
                && candidatePlan.Capacity == policy.MaxLearners
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
                && p.Capacity == policy.MaxLearners
                && p.Price == totalTuition
                && p.IsPublished)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(token);

        if (plan is null)
        {
            plan = new StudyCoursePlan
            {
                SessionType = "Group",
                Capacity = policy.MaxLearners,
                Name = $"Lớp nhóm JLPT {levelCode} · {weeklySessions} buổi/tuần · {durationWeeks} tuần ({totalSessions} buổi)",
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
            CourseDurationWeeks = durationWeeks,
            EnrollmentOpensAtUtc = enrollmentOpensAtUtc,
            EnrollmentClosesAtUtc = enrollmentClosesAtUtc,
            GroupPolicyId = policy.Id,
            PolicyVersionSnapshot = policy.Version,
            SourceProposalId = proposal.Id,
            TuitionPerSessionSnapshot = tuitionPerSession,
            TotalTuitionSnapshot = totalTuition,
            TuitionPolicyVersionSnapshot = policy.Version,
            TuitionSnapshotAtUtc = enrollmentOpensAtUtc,
            CreatedAtUtc = enrollmentOpensAtUtc,
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

    // ───────────────────────────── CORE VALIDATION & UNIFIED SCHEDULE ENGINE ─────────────────────────────

    private sealed record ProposalCandidate(
        int? ProposalId,
        int MentorId,
        int? CertificateLevelId,
        string ProgramType,
        int? CoursePlanId,
        string? CourseObjective,
        int CourseDurationWeeks,
        int SessionDurationMinutes,
        List<WeeklySessionSlotDto> WeeklySessions,
        List<ProposalAttachmentDto> OptionalAttachments,
        DateOnly? ReferenceStartDate = null);

    private async Task<(string? LevelCode, List<ProposalValidationIssue> Issues)> EvaluateProposalStructureAsync(
        ProposalCandidate candidate,
        MentorGroupPolicy policy,
        CancellationToken ct)
    {
        var issues = new List<ProposalValidationIssue>();
        var restMinutes = Math.Max(0, policy.RestMinutes);
        var requiredSessionMinutes = policy.SessionMinutes;
        string? resolvedLevelCode = null;

        // 1. Xác minh tư cách Giáo viên & quyền giảng dạy theo danh mục cấp độ động
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
                "Giáo viên chưa có trình độ chuyên môn được xác minh để đăng ký mở lớp."));
        }

        // 2. Trình độ JLPT (chỉ kiểm tra quyền giảng dạy & đơn giá học phí, KHÔNG giới hạn số lớp cùng cấp độ)
        if (!candidate.CertificateLevelId.HasValue || candidate.CertificateLevelId.Value <= 0)
        {
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
                    $"Hiện tại hệ thống chỉ hỗ trợ mở lớp nhóm ở các cấp độ N5–N2 (cấp độ {levelRow.Code} chưa được áp dụng)."));
            }
            else
            {
                resolvedLevelCode = levelRow.Code;
                var hasDirectQualification = verifiedQualCodes.Any(c =>
                    string.Equals(c?.Trim(), levelRow.Code?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (mentorVerified
                    && highestVerifiedRank > 0
                    && !hasDirectQualification
                    && !MentorSlotPolicy.CanMentorTeachLevel(highestVerifiedRank, levelRow.Code))
                {
                    issues.Add(Error(
                        "INSUFFICIENT_TEACHING_QUALIFICATION",
                        $"Bạn chưa được cấp quyền giảng dạy cấp độ {levelRow.Code}. Trình độ đã xác minh hiện tại là {highestVerifiedCode}."));
                }

                if (policy.GetTuitionPerSession(levelRow.Code) <= 0)
                {
                    issues.Add(Error(
                        "TUITION_NOT_CONFIGURED",
                        $"Học phí cho trình độ JLPT {levelRow.Code} chưa được cấu hình hợp lệ. Vui lòng liên hệ Quản trị viên."));
                }
            }
        }

        // 3. Mục tiêu khóa học không bắt buộc (tối đa 1500 ký tự, được chuẩn hóa qua TrimOrNull)

        // 4. Số tuần giảng dạy linh hoạt (>= 1 tuần, giới hạn kỹ thuật tối đa 104 tuần và tối đa 1000 buổi)
        if (candidate.CourseDurationWeeks < MentorSlotPolicy.MinCourseDurationWeeks
            || candidate.CourseDurationWeeks > MentorSlotPolicy.MaxCourseDurationWeeksTechnical)
        {
            issues.Add(Error("PROPOSAL_DURATION_INVALID", "Vui lòng nhập số tuần giảng dạy hợp lệ."));
        }

        var sessions = candidate.WeeklySessions;
        if (sessions.Count == 0)
        {
            issues.Add(Error("PROPOSAL_EMPTY_SCHEDULE", "Vui lòng chọn ít nhất một khung giờ."));
        }
        else if (candidate.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks
            && (long)sessions.Count * candidate.CourseDurationWeeks > MentorSlotPolicy.MaxTotalCourseSessionsTechnical)
        {
            issues.Add(Error(
                "PROPOSAL_TOTAL_SESSIONS_TOO_LARGE",
                $"Tổng số buổi dự kiến ({(long)sessions.Count * candidate.CourseDurationWeeks} buổi) vượt quá mức hệ thống có thể khởi tạo một lần ({MentorSlotPolicy.MaxTotalCourseSessionsTechnical} buổi). Vui lòng giảm số tuần hoặc chia thành nhiều lớp."));
        }

        // 5. Tính hợp lệ từng khung giờ, trùng lặp và khoảng nghỉ trong tuần của chính đề xuất
        var seenSlotKeys = new HashSet<(int, int)>();
        foreach (var s in sessions)
        {
            if (s.Weekday is < 0 or > 6)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", "Thứ trong tuần không hợp lệ.", s.Weekday, s.StartMinute));
            else if (s.EndMinute - s.StartMinute != requiredSessionMinutes)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Mỗi buổi dạy cần có thời lượng đúng {requiredSessionMinutes} phút.", s.Weekday, s.StartMinute));
            else if (s.StartMinute < DefaultOperatingStartMinute || s.EndMinute > DefaultOperatingEndMinute)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Buổi {WeekdayLabel(s.Weekday)} ({Clock(s.StartMinute)}–{Clock(s.EndMinute)}) nằm ngoài khung giờ giảng dạy cho phép.", s.Weekday, s.StartMinute));
            else if (s.StartMinute % DefaultGridStepMinutes != 0)
                issues.Add(Error("PROPOSAL_INVALID_SESSION_SLOT", $"Giờ bắt đầu {Clock(s.StartMinute)} phải chẵn theo bước {DefaultGridStepMinutes} phút.", s.Weekday, s.StartMinute));
            else if (!seenSlotKeys.Add((s.Weekday, s.StartMinute)))
                issues.Add(Error("PROPOSAL_OWN_OVERLAP", $"Khung giờ {WeekdayLabel(s.Weekday)} {Clock(s.StartMinute)}–{Clock(s.EndMinute)} bị chọn trùng.", s.Weekday, s.StartMinute));
        }

        var weeklyIssues = ValidateWeeklyCycleRest(sessions, restMinutes, candidate.CourseDurationWeeks);
        issues.AddRange(weeklyIssues);

        return (resolvedLevelCode, issues);
    }

    /// <summary>
    /// Kiểm tra trùng lặp, chồng lấn và khoảng nghỉ tối thiểu trong tuần (bao gồm ranh giới cuối ngày sang đầu ngày hôm sau
    /// và ranh giới Chủ nhật sang Thứ 2 tuần kế tiếp nếu khóa học >= 2 tuần).
    /// </summary>
    private static List<ProposalValidationIssue> ValidateWeeklyCycleRest(
        IReadOnlyList<WeeklySessionSlotDto> sessions,
        int restMinutes,
        int durationWeeks)
    {
        var issues = new List<ProposalValidationIssue>();
        if (sessions.Count <= 1)
            return issues;

        var timeline = sessions
            .Select(s =>
            {
                var monOffset = MentorSlotPolicy.GetMondayOffset(s.Weekday);
                var absStart = monOffset * 1440 + s.StartMinute;
                var absEnd = monOffset * 1440 + s.EndMinute;
                return (Slot: s, AbsStart: absStart, AbsEnd: absEnd);
            })
            .OrderBy(x => x.AbsStart)
            .ThenBy(x => x.AbsEnd)
            .ToList();

        for (var i = 1; i < timeline.Count; i++)
        {
            var prev = timeline[i - 1];
            var curr = timeline[i];
            var sameDay = prev.Slot.Weekday == curr.Slot.Weekday;

            if (curr.AbsStart < prev.AbsEnd)
            {
                issues.Add(Error("PROPOSAL_OWN_OVERLAP",
                    $"Hai buổi {(sameDay ? $"trong {WeekdayLabel(curr.Slot.Weekday)}" : $"{WeekdayLabel(prev.Slot.Weekday)} và {WeekdayLabel(curr.Slot.Weekday)}")} bị chồng giờ ({Clock(prev.Slot.StartMinute)}–{Clock(prev.Slot.EndMinute)} và {Clock(curr.Slot.StartMinute)}–{Clock(curr.Slot.EndMinute)}).",
                    curr.Slot.Weekday, curr.Slot.StartMinute));
            }
            else if (restMinutes > 0 && curr.AbsStart - prev.AbsEnd < restMinutes)
            {
                issues.Add(Error("PROPOSAL_OWN_REST_VIOLATION",
                    sameDay
                        ? $"Hai buổi trong {WeekdayLabel(curr.Slot.Weekday)} quá sát nhau ({Clock(prev.Slot.StartMinute)}–{Clock(prev.Slot.EndMinute)} và {Clock(curr.Slot.StartMinute)}–{Clock(curr.Slot.EndMinute)}); cần nghỉ tối thiểu {restMinutes} phút."
                        : $"Khoảng nghỉ giữa buổi cuối {WeekdayLabel(prev.Slot.Weekday)} ({Clock(prev.Slot.EndMinute)}) và buổi đầu {WeekdayLabel(curr.Slot.Weekday)} ({Clock(curr.Slot.StartMinute)}) phải tối thiểu {restMinutes} phút.",
                    curr.Slot.Weekday, curr.Slot.StartMinute));
            }
        }

        if (durationWeeks >= 2 && timeline.Count > 0 && restMinutes > 0)
        {
            var last = timeline[^1];
            var first = timeline[0];
            var wrapGap = (first.AbsStart + 10080) - last.AbsEnd;
            if (wrapGap < restMinutes)
            {
                issues.Add(Error("PROPOSAL_OWN_REST_VIOLATION",
                    $"Khoảng nghỉ chuyển tiếp từ buổi cuối {WeekdayLabel(last.Slot.Weekday)} ({Clock(last.Slot.EndMinute)}) sang buổi đầu {WeekdayLabel(first.Slot.Weekday)} tuần sau ({Clock(first.Slot.StartMinute)}) cần tối thiểu {restMinutes} phút.",
                    first.Slot.Weekday, first.Slot.StartMinute));
            }
        }

        return issues;
    }

    private async Task<TeachingProposalValidationResponse> EvaluateProposalCandidateAsync(
        ProposalCandidate candidate,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var (resolvedLevelCode, issues) = await EvaluateProposalStructureAsync(candidate, policy, ct);
        var workload = await ComputeMentorWorkloadAsync(candidate.MentorId, nowUtc, ct);
        var hasErrors = issues.Any(i => i.Severity == "Error");
        var weeklyCount = candidate.WeeklySessions.Count;
        var totalSessions = candidate.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks
            ? weeklyCount * candidate.CourseDurationWeeks
            : 0;
        var tuitionPerSession = !string.IsNullOrWhiteSpace(resolvedLevelCode)
            ? policy.GetTuitionPerSession(resolvedLevelCode)
            : 0m;
        var totalCourseTuition = tuitionPerSession * totalSessions;

        // Giáo viên chưa duyệt không có ngày bắt đầu/kết thúc giả hay lịch buổi cụ thể trước khi Admin phê duyệt.
        return new TeachingProposalValidationResponse
        {
            IsValid = !hasErrors,
            CanSubmit = !hasErrors && weeklyCount >= 1 && candidate.CourseDurationWeeks >= MentorSlotPolicy.MinCourseDurationWeeks,
            CourseDurationWeeks = candidate.CourseDurationWeeks,
            WeeklySessionCount = weeklyCount,
            TotalSessions = totalSessions,
            TotalTeachingMinutes = totalSessions * candidate.SessionDurationMinutes,
            TuitionPerSession = tuitionPerSession,
            TotalCourseTuition = totalCourseTuition,
            TuitionPolicyVersion = tuitionPerSession > 0 ? policy.Version : null,
            MaxLearners = policy.MaxLearners,
            ProjectedEndDate = null,
            Issues = issues,
            OccurrencesPreview = [],
            Workload = workload,
        };
    }

    /// <summary>
    /// V4 Full-Week Scheduling Engine (Thứ 2 -> Chủ nhật):
    /// Từ Hạn tuyển sinh (<paramref name="enrollmentDeadlineLocalDate"/>) và số ngày chuẩn bị tối thiểu,
    /// xác định tuần đầy đủ (Thứ 2..Chủ nhật) hợp lệ sớm nhất sao cho toàn bộ khóa học khớp khung giờ tuần
    /// và không xung đột với bất kỳ lịch chính thức nào của chính Giáo viên.
    /// </summary>
    private async Task<ResolvedApprovalSchedule> ResolveOpenEnrollmentScheduleAsync(
        MentorTeachingProposal proposal,
        DateOnly enrollmentDeadlineLocalDate,
        MentorGroupPolicy policy,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var candidate = CandidateFromEntity(proposal, policy);
        var (resolvedLevelCode, issues) = await EvaluateProposalStructureAsync(candidate, policy, ct);
        var prepDays = Math.Max(MentorSlotPolicy.MinimumPreparationDays, policy.MinimumPreparationDays);
        var restMinutes = Math.Max(0, policy.RestMinutes);
        var sessionMinutes = policy.SessionMinutes;
        var weeklyCount = candidate.WeeklySessions.Count;
        var durationWeeks = Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, candidate.CourseDurationWeeks);
        var totalSessionsNominal = weeklyCount * durationWeeks;
        var tuitionPerSession = !string.IsNullOrWhiteSpace(resolvedLevelCode)
            ? policy.GetTuitionPerSession(resolvedLevelCode)
            : 0m;
        var totalTuitionNominal = tuitionPerSession * totalSessionsNominal;
        var enrollmentClosesAtUtc = MentorSlotPolicy.ResolveEnrollmentDeadlineEndOfDayUtc(enrollmentDeadlineLocalDate, BusinessZone.Value);

        if (issues.Any(i => i.Severity == "Error"))
        {
            var msg = issues.First(i => i.Severity == "Error").Message;
            return new ResolvedApprovalSchedule(
                CanApprove: false,
                BlockingReason: msg,
                EnrollmentOpensAtUtc: nowUtc,
                EnrollmentClosesAtUtc: enrollmentClosesAtUtc,
                StartsAtUtc: nowUtc,
                EndsAtUtc: nowUtc,
                StartDate: null,
                EndDate: null,
                TotalSessions: totalSessionsNominal,
                SessionDurationMinutes: sessionMinutes,
                TotalTeachingMinutes: totalSessionsNominal * sessionMinutes,
                MaxLearners: policy.MaxLearners,
                TuitionPerSession: tuitionPerSession,
                TotalCourseTuition: totalTuitionNominal,
                PolicyVersion: policy.Version,
                WasShiftedDueToConflict: false,
                ConflictShiftReason: null,
                SchedulePreviewToken: null,
                Occurrences: [],
                Issues: issues,
                SuggestedEnrollmentDeadlineDate: null,
                SuggestedDeadlineReason: null,
                MinimumPreparationDays: prepDays);
        }

        var earliestAllowedSessionDate = enrollmentDeadlineLocalDate.AddDays(prepDays);
        var orderedWeeklyTuples = candidate.WeeklySessions
            .Select(s => (s.Weekday, s.StartMinute))
            .Distinct()
            .OrderBy(s => MentorSlotPolicy.GetMondayOffset(s.Weekday))
            .ThenBy(s => s.StartMinute)
            .ToList();

        var earliestValidMonday = MentorSlotPolicy.ResolveEarliestValidWeekMonday(
            earliestAllowedSessionDate,
            orderedWeeklyTuples);

        TeachingConflictReport? firstConflictReport = null;

        for (var weekOffset = 0; weekOffset <= MentorSlotPolicy.MaxScheduleSearchWeeks; weekOffset++)
        {
            var candidateMonday = earliestValidMonday.AddDays(weekOffset * 7);
            var expanded = MentorSlotPolicy.ExpandFullCalendarWeeks(
                candidateMonday,
                orderedWeeklyTuples,
                durationWeeks,
                sessionMinutes,
                BusinessZone.Value);

            if (expanded.Count != totalSessionsNominal || expanded[0].StartUtc <= nowUtc || expanded[0].LocalDate < earliestAllowedSessionDate)
                continue;

            var intervals = expanded
                .Select(o => new ProposedTeachingInterval(o.StartUtc, o.EndUtc, o.SessionNumber))
                .ToList();

            var report = await teachingConflictService.CheckAsync(
                proposal.MentorId,
                intervals,
                new TeachingConflictOptions
                {
                    ExcludeProposalId = proposal.Id,
                    RestMinutes = restMinutes,
                    IncludeOwnIntervals = false,
                },
                ct);

            if (report.IsValid)
            {
                var occurrences = expanded.Select(occ => new GeneratedSessionOccurrenceResponse
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

                var startsAtUtc = expanded[0].StartUtc;
                var endsAtUtc = expanded[^1].EndUtc;
                var startDate = expanded[0].LocalDate;
                var endDate = expanded[^1].LocalDate;
                var shifted = weekOffset > 0;
                var shiftReason = shifted && firstConflictReport?.First is not null
                    ? $"Tuần bắt đầu sớm nhất trùng lịch dạy hiện có ({firstConflictReport.First.Message}); hệ thống đã dời sang tuần đầy đủ khả dụng tiếp theo từ {startDate:dd/MM/yyyy}."
                    : null;

                DateOnly? suggestedDeadline = null;
                string? suggestedReason = null;
                if (shifted)
                {
                    var candidateSuggested = startDate.AddDays(-prepDays);
                    if (candidateSuggested > enrollmentDeadlineLocalDate)
                    {
                        suggestedDeadline = candidateSuggested;
                        suggestedReason = $"Lịch học được xếp từ ngày {startDate:dd/MM/yyyy} — bạn có thể gia hạn hạn tuyển sinh đến hết {candidateSuggested:dd/MM/yyyy} để tối ưu thời gian tuyển học viên.";
                    }
                }

                var previewIssues = new List<ProposalValidationIssue>(issues);
                if (shifted && firstConflictReport is not null)
                {
                    foreach (var c in firstConflictReport.Conflicts.Take(6))
                        previewIssues.Add(MapConflictIssue(c));
                }

                var token = ComputeSchedulePreviewToken(
                    policy.Id,
                    policy.Version,
                    policy.Revision,
                    proposal.Id,
                    proposal.Revision,
                    enrollmentDeadlineLocalDate,
                    startsAtUtc,
                    endsAtUtc,
                    enrollmentClosesAtUtc,
                    expanded.Count,
                    policy.MaxLearners,
                    totalTuitionNominal);

                return new ResolvedApprovalSchedule(
                    CanApprove: true,
                    BlockingReason: null,
                    EnrollmentOpensAtUtc: nowUtc,
                    EnrollmentClosesAtUtc: enrollmentClosesAtUtc,
                    StartsAtUtc: startsAtUtc,
                    EndsAtUtc: endsAtUtc,
                    StartDate: startDate,
                    EndDate: endDate,
                    TotalSessions: expanded.Count,
                    SessionDurationMinutes: sessionMinutes,
                    TotalTeachingMinutes: expanded.Count * sessionMinutes,
                    MaxLearners: policy.MaxLearners,
                    TuitionPerSession: tuitionPerSession,
                    TotalCourseTuition: totalTuitionNominal,
                    PolicyVersion: policy.Version,
                    WasShiftedDueToConflict: shifted,
                    ConflictShiftReason: shiftReason,
                    SchedulePreviewToken: token,
                    Occurrences: occurrences,
                    Issues: previewIssues,
                    SuggestedEnrollmentDeadlineDate: suggestedDeadline,
                    SuggestedDeadlineReason: suggestedReason,
                    MinimumPreparationDays: prepDays);
            }

            firstConflictReport ??= report;
        }

        var conflictIssues = new List<ProposalValidationIssue>(issues);
        if (firstConflictReport is not null)
        {
            foreach (var c in firstConflictReport.Conflicts.Take(10))
                conflictIssues.Add(MapConflictIssue(c));
        }

        var blockingMessage = $"Không tìm thấy tuần giảng dạy trống phù hợp cho Giáo viên trong {MentorSlotPolicy.MaxScheduleSearchWeeks} tuần tiếp theo mà vẫn giữ nguyên lịch tuần đã duyệt.";
        if (firstConflictReport is null)
            conflictIssues.Add(Error("PROPOSAL_NO_AVAILABLE_SCHEDULE_WINDOW", blockingMessage));

        return new ResolvedApprovalSchedule(
            CanApprove: false,
            BlockingReason: blockingMessage,
            EnrollmentOpensAtUtc: nowUtc,
            EnrollmentClosesAtUtc: enrollmentClosesAtUtc,
            StartsAtUtc: LocalDateMinuteToUtc(earliestAllowedSessionDate, 0),
            EndsAtUtc: LocalDateMinuteToUtc(earliestAllowedSessionDate, 0),
            StartDate: null,
            EndDate: null,
            TotalSessions: totalSessionsNominal,
            SessionDurationMinutes: sessionMinutes,
            TotalTeachingMinutes: totalSessionsNominal * sessionMinutes,
            MaxLearners: policy.MaxLearners,
            TuitionPerSession: tuitionPerSession,
            TotalCourseTuition: totalTuitionNominal,
            PolicyVersion: policy.Version,
            WasShiftedDueToConflict: false,
            ConflictShiftReason: null,
            SchedulePreviewToken: null,
            Occurrences: [],
            Issues: conflictIssues,
            SuggestedEnrollmentDeadlineDate: null,
            SuggestedDeadlineReason: null,
            MinimumPreparationDays: prepDays);
    }

    private static ApprovalSchedulePreviewResponse ToOpenEnrollmentPreviewResponse(
        ResolvedApprovalSchedule resolved,
        DateOnly requestedDeadlineDate,
        DateOnly minimumDeadlineDate,
        int weeklySessionCount,
        int courseDurationWeeks)
    {
        return new ApprovalSchedulePreviewResponse
        {
            CanApprove = resolved.CanApprove,
            BlockingReason = resolved.BlockingReason,
            EnrollmentOpensAtUtc = resolved.EnrollmentOpensAtUtc,
            EnrollmentClosesAtUtc = resolved.EnrollmentClosesAtUtc,
            StartsAtUtc = resolved.StartsAtUtc,
            EndsAtUtc = resolved.EndsAtUtc,
            StartDate = resolved.StartDate,
            EndDate = resolved.EndDate,
            RequestedEnrollmentDeadlineDate = requestedDeadlineDate,
            MinimumEnrollmentDeadlineDate = minimumDeadlineDate,
            SuggestedEnrollmentDeadlineDate = resolved.SuggestedEnrollmentDeadlineDate,
            SuggestedDeadlineReason = resolved.SuggestedDeadlineReason,
            MinimumPreparationDays = resolved.MinimumPreparationDays,
            WeeklySessionCount = weeklySessionCount,
            CourseDurationWeeks = courseDurationWeeks,
            TotalSessions = resolved.TotalSessions,
            SessionDurationMinutes = resolved.SessionDurationMinutes,
            TotalTeachingMinutes = resolved.TotalTeachingMinutes,
            MaxLearners = resolved.MaxLearners,
            TuitionPerSession = resolved.TuitionPerSession,
            TotalCourseTuition = resolved.TotalCourseTuition,
            PolicyVersion = resolved.PolicyVersion,
            WasShiftedDueToConflict = resolved.WasShiftedDueToConflict,
            ConflictShiftReason = resolved.ConflictShiftReason,
            Conflicts = resolved.Issues.Where(i => i.Severity == "Error").ToList(),
            SchedulePreviewToken = resolved.SchedulePreviewToken,
            Occurrences = resolved.Occurrences,
        };
    }

    private static string ComputeSchedulePreviewToken(
        int policyId,
        int policyVersion,
        int policyRevision,
        int proposalId,
        int proposalRevision,
        DateOnly deadlineLocalDate,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime enrollmentClosesAtUtc,
        int totalSessions,
        int maxLearners,
        decimal totalTuition)
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{policyId}|{policyVersion}|{policyRevision}|{proposalId}|{proposalRevision}|{deadlineLocalDate:yyyy-MM-dd}|{startsAtUtc:O}|{endsAtUtc:O}|{enrollmentClosesAtUtc:O}|{totalSessions}|{maxLearners}|{totalTuition:0}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
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
        int mentorId, DateTime nowUtc, CancellationToken ct)
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
            })
            .ToListAsync(ct);

        var pendingProps = proposals.Where(p => p.Status == MentorTeachingProposalStatus.PendingReview).ToList();
        var changesRequestedProps = proposals.Where(p => p.Status == MentorTeachingProposalStatus.ChangesRequested).ToList();
        var approvedReadyProps = proposals.Where(p =>
            p.Status == MentorTeachingProposalStatus.Approved
            && !linkedProposalIds.Contains(p.Id)).ToList();

        var pendingWeeklySessions = pendingProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count)
            + changesRequestedProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count);
        // Trong V4, đề xuất Đã duyệt chưa mở tuyển sinh KHÔNG giữ chỗ và KHÔNG chiếm công suất lịch chính thức.
        var approvedCount = approvedReadyProps.Count;
        var draftCount = proposals.Any(p => p.Status == MentorTeachingProposalStatus.Draft && DeserializeSessions(p.WeeklySessionsJson).Count > 0) ? 1 : 0;

        var weekStart = WeekStartUtc(nowUtc);
        var upcomingSlots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Where(a => a.MentorId == mentorId && !a.IsDeleted && a.RejectedAt == null && a.IsApproved && a.StartTime >= weekStart)
            .Select(a => new { a.Id, a.StartTime })
            .ToListAsync(ct);

        var peakDay = upcomingSlots
            .Where(a => !cancelledAvSet.Contains(a.Id))
            .GroupBy(a => DateOnly.FromDateTime(LocalTime(MentorTeachingConflictService.Utc(a.StartTime))))
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .Max();

        var weeklyCommitted = activeCohorts.Sum(c => c.SessionsPerWeek);

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
        };
    }

    private static ProposalValidationIssue MapConflictIssue(TeachingConflict c)
    {
        var mappedCode = c.Code switch
        {
            MentorTeachingConflictService.RestViolation => "PROPOSAL_REST_VIOLATION",
            _ => "PROPOSAL_CONFLICT_APPROVED_SCHEDULE",
        };

        var localStart = LocalTime(c.ProposedStartUtc);
        var conflictStart = c.CommitmentStartUtc.HasValue ? LocalTime(c.CommitmentStartUtc.Value) : (DateTime?)null;
        var conflictEnd = c.CommitmentEndUtc.HasValue ? LocalTime(c.CommitmentEndUtc.Value) : (DateTime?)null;

        return new ProposalValidationIssue
        {
            Code = mappedCode,
            Severity = "Error",
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

    private static bool IsConflictCode(string? code) => code is
        "PROPOSAL_CONFLICT_APPROVED_SCHEDULE"
        or "PROPOSAL_REST_VIOLATION"
        or "PROPOSAL_CONFLICT_PENDING_PROPOSAL"
        or "PROPOSAL_PENDING_REST_VIOLATION"
        or "PROPOSAL_OWN_OVERLAP"
        or "PROPOSAL_OWN_REST_VIOLATION"
        or "APPROVAL_PREVIEW_STALE";

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
        string? CancellationReason,
        DateTime? EnrollmentClosesAtUtc,
        DateTime StartsAt,
        DateTime EndsAt,
        int Capacity,
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
                c.CancellationReason,
                c.EnrollmentClosesAtUtc,
                c.StartsAt,
                c.EndsAt,
                Capacity = c.CapacitySnapshot ?? c.Capacity,
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
                r.CancellationReason,
                r.EnrollmentClosesAtUtc,
                r.StartsAt,
                r.EndsAt,
                r.Capacity,
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
        bool includeHistory,
        bool isAdminView)
    {
        var resolvedWeeks = Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, p.CourseDurationWeeks);
        var resolvedMinutes = p.SessionDurationMinutes > 0 ? p.SessionDurationMinutes : policy.SessionMinutes;
        var sessions = DeserializeSessions(p.WeeklySessionsJson, resolvedMinutes);
        var attachments = DeserializeAttachments(p.OptionalAttachmentsJson);

        var isOpened = p.Status == MentorTeachingProposalStatus.OpenedClass
            || (p.Status == MentorTeachingProposalStatus.Approved && linkedCohort is not null);
        var openedSlots = isOpened
            ? p.GeneratedAvailabilities
                .Where(a => !a.IsDeleted && a.IsApproved && a.RejectedAt == null)
                .OrderBy(a => a.StartTime)
                .ToList()
            : [];

        var occurrences = new List<GeneratedSessionOccurrenceResponse>();
        if (isOpened && openedSlots.Count > 0)
        {
            var perCycle = Math.Max(1, sessions.Count);
            for (var i = 0; i < openedSlots.Count; i++)
            {
                var slot = openedSlots[i];
                var startUtc = MentorTeachingConflictService.Utc(slot.StartTime);
                var endUtc = MentorTeachingConflictService.Utc(slot.EndTime);
                var localStart = LocalTime(startUtc);
                var localEnd = LocalTime(endUtc);
                occurrences.Add(new GeneratedSessionOccurrenceResponse
                {
                    SessionNumber = i + 1,
                    WeekNumber = (i / perCycle) + 1,
                    Weekday = (int)localStart.DayOfWeek,
                    LocalDate = localStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    LocalStart = localStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                    LocalEnd = localEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                });
            }
        }
        else if (isOpened && p.ExpectedStartDate.HasValue && sessions.Count > 0)
        {
            occurrences = ExpandOccurrences(p.ExpectedStartDate.Value, sessions, resolvedWeeks, resolvedMinutes);
        }

        DateOnly? officialStartDate = isOpened
            ? (occurrences.Count > 0
                ? DateOnly.ParseExact(occurrences[0].LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : (linkedCohort is not null
                    ? DateOnly.FromDateTime(LocalTime(linkedCohort.StartsAt))
                    : p.ExpectedStartDate))
            : null;
        DateOnly? officialEndDate = isOpened
            ? (occurrences.Count > 0
                ? DateOnly.ParseExact(occurrences[^1].LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : (linkedCohort is not null
                    ? DateOnly.FromDateTime(LocalTime(linkedCohort.EndsAt))
                    : null))
            : null;

        DateTime? enrollmentClosesAtUtc = isOpened
            ? (linkedCohort?.EnrollmentClosesAtUtc
                ?? (occurrences.Count > 0 ? occurrences[0].StartUtc : null))
            : null;

        var totalSessions = isOpened && occurrences.Count > 0
            ? occurrences.Count
            : sessions.Count * resolvedWeeks;
        var tuitionPerSession = linkedCohort?.TuitionPerSessionSnapshot is decimal snapPer && snapPer > 0
            ? snapPer
            : policy.GetTuitionPerSession(p.CertificateLevel?.Code);
        var totalCourseTuition = linkedCohort?.TotalTuitionSnapshot is decimal snapTotal && snapTotal > 0
            ? snapTotal
            : tuitionPerSession * totalSessions;
        var tuitionPolicyVersion = linkedCohort?.TuitionPolicyVersionSnapshot
            ?? (tuitionPerSession > 0 ? policy.Version : (int?)null);
        var maxLearners = linkedCohort is { Capacity: > 0 } ? linkedCohort.Capacity : policy.MaxLearners;

        var mentorDisplayName = p.Mentor is null
            ? "Giáo viên"
            : $"{p.Mentor.FirstName} {p.Mentor.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(mentorDisplayName))
            mentorDisplayName = p.Mentor?.Email ?? "Giáo viên";

        return new TeachingProposalResponse
        {
            Id = p.Id,
            MentorId = p.MentorId,
            MentorName = mentorDisplayName,
            MentorEmail = isAdminView ? (p.Mentor?.Email ?? string.Empty) : string.Empty,
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
            ExpectedStartDate = officialStartDate,
            ReferenceStartDate = isOpened ? null : p.ExpectedStartDate,
            ProjectedEndDate = officialEndDate,
            EnrollmentClosesAtUtc = enrollmentClosesAtUtc,
            CourseDurationWeeks = resolvedWeeks,
            SessionDurationMinutes = resolvedMinutes,
            WeeklySessionCount = sessions.Count,
            TotalSessions = totalSessions,
            TotalTeachingMinutes = totalSessions * resolvedMinutes,
            MaxLearners = maxLearners,
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
            OpenedClassAtUtc = p.OpenedClassAtUtc,
            ReviewedByAdminId = p.ReviewedByAdminId,
            ReviewedByAdminName = p.ReviewedByAdmin is null ? null : $"{p.ReviewedByAdmin.FirstName} {p.ReviewedByAdmin.LastName}".Trim(),
            ReviewReason = p.ReviewReason,
            CreatedAtUtc = p.CreatedAtUtc,
            UpdatedAtUtc = p.UpdatedAtUtc,
            LastSavedAtUtc = p.LastSavedAtUtc,
            GeneratedAvailabilityIds = openedSlots.Select(a => a.Id).ToList(),
            LinkedCohortId = linkedCohort is { Id: > 0 } lc ? lc.Id : null,
            LinkedCohortName = linkedCohort is { Id: > 0 } lc2 ? lc2.Name : null,
            LinkedCohortStatus = linkedCohort is { Id: > 0 } lc3 ? lc3.Status : null,
            LinkedCohortCancellationReason = linkedCohort is { Id: > 0 } lc4 ? lc4.CancellationReason : null,
            LinkedCohortStartsAtUtc = linkedCohort is { Id: > 0 } lc5 ? lc5.StartsAt : null,
            LinkedCohortEndsAtUtc = linkedCohort is { Id: > 0 } lc6 ? lc6.EndsAt : null,
            LinkedCohortEnrollmentClosesAtUtc = linkedCohort is { Id: > 0 } lc7 ? lc7.EnrollmentClosesAtUtc : null,
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

    private static TeachingProposalPolicyResponse MapPolicy(MentorGroupPolicy p) => new()
    {
        PolicyId = p.Id,
        PolicyVersion = p.Version,
        TimeZoneId = MentorSlotPolicy.BusinessTimeZoneId,
        SessionDurationMinutes = p.SessionMinutes,
        RestMinutes = p.RestMinutes,
        MaxLearners = p.MaxLearners,
        GridStepMinutes = DefaultGridStepMinutes,
        AllowedGridSteps = [15, 30],
        OperatingStartMinute = DefaultOperatingStartMinute,
        OperatingEndMinute = DefaultOperatingEndMinute,
        MinimumPreparationDays = p.MinimumPreparationDays,
        MinimumPublishedEnrollmentDays = p.MinimumPublishedEnrollmentDays,
        CheckoutHoldMinutes = p.CheckoutHoldMinutes,
        MinCourseDurationWeeks = MentorSlotPolicy.MinCourseDurationWeeks,
        MaxCourseDurationWeeksTechnical = MentorSlotPolicy.MaxCourseDurationWeeksTechnical,
        MaxTotalCourseSessionsTechnical = MentorSlotPolicy.MaxTotalCourseSessionsTechnical,
        TuitionPerSessionByLevel = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["N5"] = p.TuitionPerSessionN5,
            ["N4"] = p.TuitionPerSessionN4,
            ["N3"] = p.TuitionPerSessionN3,
            ["N2"] = p.TuitionPerSessionN2,
        },
    };

    private static (string Code, string Message)? ValidateDraftFields(
        SaveTeachingProposalDraftRequest request,
        MentorGroupPolicy policy,
        out string normalizedProgram,
        out int normalizedWeeks,
        out List<WeeklySessionSlotDto> normalizedSessions,
        out List<ProposalAttachmentDto> normalizedAttachments)
    {
        normalizedProgram = NormalizeProgramType(request.ProgramType);
        normalizedWeeks = request.CourseDurationWeeks ?? MentorSlotPolicy.MinCourseDurationWeeks;
        normalizedSessions = [];
        normalizedAttachments = [];

        if (normalizedWeeks < MentorSlotPolicy.MinCourseDurationWeeks
            || normalizedWeeks > MentorSlotPolicy.MaxCourseDurationWeeksTechnical)
        {
            return ("PROPOSAL_DURATION_INVALID", "Vui lòng nhập số tuần giảng dạy hợp lệ.");
        }

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
                return ("PROPOSAL_INVALID_SESSION_SLOT", $"Buổi {WeekdayLabel(raw.Weekday)} ({Clock(raw.StartMinute)}–{Clock(endMin)}) nằm ngoài khung giờ giảng dạy cho phép.");
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

        if (normalizedSessions.Count > 0
            && (long)normalizedSessions.Count * normalizedWeeks > MentorSlotPolicy.MaxTotalCourseSessionsTechnical)
        {
            return ("PROPOSAL_TOTAL_SESSIONS_TOO_LARGE",
                $"Tổng số buổi dự kiến ({(long)normalizedSessions.Count * normalizedWeeks} buổi) vượt quá mức hệ thống hỗ trợ cho một lớp ({MentorSlotPolicy.MaxTotalCourseSessionsTechnical} buổi).");
        }

        var weeklyDraftErrors = ValidateWeeklyCycleRest(normalizedSessions, restMinutes, normalizedWeeks);
        if (weeklyDraftErrors.Count > 0)
            return (weeklyDraftErrors[0].Code, weeklyDraftErrors[0].Message);

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

                var cleanName = Path.GetFileName(att.Name.Trim());
                var declaredNameExt = Path.GetExtension(cleanName);
                if (!string.IsNullOrWhiteSpace(declaredNameExt)
                    && !AttachmentContentInspector.AreExtensionsEquivalent(declaredNameExt, extCandidate))
                {
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Tên tệp đính kèm không khớp với định dạng tệp đã tải lên.");
                }

                if (att.SizeBytes is < 0 or > MaxAttachmentBytes)
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Dung lượng tệp đính kèm vượt quá giới hạn 25 MB.");

                var resolvedContentType = TrimOrNull(att.ContentType, 120);
                if (string.IsNullOrWhiteSpace(resolvedContentType)
                    || string.Equals(resolvedContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedContentType = AttachmentContentInspector.ResolveCanonicalContentType(extCandidate);
                }

                normalizedAttachments.Add(new ProposalAttachmentDto
                {
                    Name = cleanName[..Math.Min(cleanName.Length, 200)],
                    Url = url[..Math.Min(url.Length, 1000)],
                    StoredFileName = TrimOrNull(att.StoredFileName, 260),
                    ContentType = resolvedContentType,
                    SizeBytes = att.SizeBytes,
                });
            }
        }

        return null;
    }

    private static ProposalCandidate CandidateFromEntity(MentorTeachingProposal p, MentorGroupPolicy policy)
    {
        var sessionMinutes = policy.SessionMinutes > 0 ? policy.SessionMinutes : 90;
        var weeks = Math.Max(MentorSlotPolicy.MinCourseDurationWeeks, p.CourseDurationWeeks);
        return new ProposalCandidate(
            ProposalId: p.Id,
            MentorId: p.MentorId,
            CertificateLevelId: p.CertificateLevelId,
            ProgramType: NormalizeProgramType(p.ProgramType),
            CoursePlanId: p.CoursePlanId,
            CourseObjective: TrimOrNull(p.CourseObjective, 1500),
            CourseDurationWeeks: weeks,
            SessionDurationMinutes: sessionMinutes,
            WeeklySessions: DeserializeSessions(p.WeeklySessionsJson, sessionMinutes),
            OptionalAttachments: DeserializeAttachments(p.OptionalAttachmentsJson),
            ReferenceStartDate: p.ExpectedStartDate);
    }

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

    private static List<GeneratedSessionOccurrenceResponse> ExpandOccurrences(
        DateOnly startDate,
        List<WeeklySessionSlotDto> weeklySessions,
        int durationWeeks,
        int sessionMinutes)
    {
        if (weeklySessions.Count == 0 || durationWeeks <= 0) return [];
        var tuples = weeklySessions.Select(s => (s.Weekday, s.StartMinute)).ToList();
        var weekMonday = MentorSlotPolicy.GetMondayOfWeek(startDate);
        var expanded = MentorSlotPolicy.ExpandFullCalendarWeeks(
            weekMonday,
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

    private static string BuildSnapshotJson(MentorTeachingProposal p)
        => JsonSerializer.Serialize(new
        {
            p.CertificateLevelId,
            p.ProgramType,
            p.CoursePlanId,
            p.CourseTitle,
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
