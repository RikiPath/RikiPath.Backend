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
    IMentorTeachingConflictService teachingConflictService)
    : IMentorTeachingProposalService
{
    private const int DefaultOperatingStartMinute = 7 * 60;   // 07:00 VN
    private const int DefaultOperatingEndMinute = 22 * 60;    // 22:00 VN
    private const int DefaultGridStepMinutes = 15;
    private const int MaxOptionalAttachments = 5;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<TimeZoneInfo> BusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));

    public async Task<ApiResponse<MentorTeachingProposalContextResponse>> GetMentorContextAsync(CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<MentorTeachingProposalContextResponse>.Fail(
                "Chỉ Giáo viên mới có thể truy cập không gian đề xuất lịch dạy.",
                HttpStatusCode.Forbidden,
                "FORBIDDEN_ROLE");

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        var policyDto = MapPolicy(policy);

        var mentor = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Id == claims.Id && !u.IsDeleted, ct);
        if (mentor is null)
            return ApiResponse<MentorTeachingProposalContextResponse>.NotFound("Không tìm thấy tài khoản Giáo viên.");

        var existingQualMap = await unitOfWork.MentorTeachingQualifications.Query().AsNoTracking()
            .Where(q => q.MentorUserId == claims.Id && q.IsActive && q.RevokedAtUtc == null)
            .ToDictionaryAsync(q => q.CertificateLevelId, q => (DateTime?)q.VerifiedAtUtc, ct);

        var profileJlptCode = mentor.MentorProfile?.JlptLevel?.Trim();
        var activeLevelsRaw = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Include(l => l.CertificateType)
            .Where(l => !l.IsDeleted && l.CertificateType != null && !l.CertificateType.IsDeleted && l.CertificateType.IsActive)
            .OrderBy(l => l.CertificateType.SortOrder)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Code, l.Description })
            .ToListAsync(ct);

        var availableLevels = activeLevelsRaw
            .Select(l => new QualifiedJlptLevelItem
            {
                CertificateLevelId = l.Id,
                Code = l.Code,
                Name = string.IsNullOrWhiteSpace(l.Description) ? $"JLPT {l.Code}" : l.Description,
                VerifiedAtUtc = existingQualMap.GetValueOrDefault(l.Id),
                IsRecommendedByProfile = !string.IsNullOrWhiteSpace(profileJlptCode)
                    && string.Equals(l.Code, profileJlptCode, StringComparison.OrdinalIgnoreCase),
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

        var proposals = await QueryProposalsWithIncludes()
            .Where(p => p.MentorId == claims.Id)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync(ct);

        var workload = await ComputeMentorWorkloadAsync(claims.Id, policy, nowUtc, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync(proposals.Select(p => p.Id).ToList(), ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);

        var mappedProposals = proposals
            .Select(p => MapProposal(p, policy, qualCodes, linkedCohorts.GetValueOrDefault(p.Id), includeHistory: false))
            .ToList();

        return ApiResponse<MentorTeachingProposalContextResponse>.Success(new MentorTeachingProposalContextResponse
        {
            MentorVerified = mentor.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved,
            MentorVerificationStatus = mentor.MentorProfile?.VerificationStatus.ToString(),
            MentorDeclaredJlptLevel = profileJlptCode,
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

        var policy = await groupPolicyService.GetEffectiveAsync(DateTime.UtcNow, ct);
        var query = QueryProposalsWithIncludes().Where(p => p.MentorId == claims.Id);
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
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, policy, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> CreateDraftAsync(SaveTeachingProposalDraftRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể tạo đề xuất mở lớp.", HttpStatusCode.Forbidden);

        request ??= new SaveTeachingProposalDraftRequest();
        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

        var sanitizeErr = ValidateDraftFields(request, policy, out var normalizedProgram, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

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

        await unitOfWork.MentorTeachingProposals.AddAsync(proposal, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == proposal.Id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    public async Task<ApiResponse<TeachingProposalResponse>> UpdateDraftAsync(int id, SaveTeachingProposalDraftRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể lưu bản nháp đề xuất.", HttpStatusCode.Forbidden);

        if (request is null)
            return ApiResponse<TeachingProposalResponse>.Fail("Dữ liệu bản nháp không hợp lệ.");

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        var sanitizeErr = ValidateDraftFields(request, policy, out var normalizedProgram, out var normalizedSessions, out var normalizedAttachments);
        if (sanitizeErr.HasValue)
            return ApiResponse<TeachingProposalResponse>.Fail(sanitizeErr.Value.Message, HttpStatusCode.BadRequest, sanitizeErr.Value.Code);

        var proposal = await unitOfWork.MentorTeachingProposals.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        if (proposal.Status != MentorTeachingProposalStatus.Draft && proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đã gửi duyệt hoặc đã đóng không thể chỉnh sửa trực tiếp.",
                HttpStatusCode.Conflict,
                "PROPOSAL_IMMUTABLE_STATUS");

        if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != proposal.Revision)
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Bản nháp đã được cập nhật ở phiên làm việc hoặc thiết bị khác. Vui lòng tải lại bản mới nhất trước khi tiếp tục.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");

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

        try
        {
            unitOfWork.MentorTeachingProposals.Update(proposal);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Phát hiện xung đột cập nhật đồng thời. Vui lòng tải lại bản nháp mới nhất.",
                HttpStatusCode.Conflict,
                "PROPOSAL_REVISION_CONFLICT");
        }

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == proposal.Id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Success(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    public async Task<ApiResponse<bool>> DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<bool>.Fail("Chỉ Giáo viên mới có thể xóa bản nháp.", HttpStatusCode.Forbidden);

        var proposal = await unitOfWork.MentorTeachingProposals.Query()
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, ct);
        if (proposal is null)
            return ApiResponse<bool>.NotFound("Không tìm thấy bản nháp.", "PROPOSAL_NOT_FOUND");

        if (proposal.Status != MentorTeachingProposalStatus.Draft)
            return ApiResponse<bool>.Fail(
                "Chỉ có thể xóa bản nháp chưa từng gửi duyệt. Với đề xuất đang chờ duyệt, hãy chọn Rút đề xuất.",
                HttpStatusCode.Conflict,
                "PROPOSAL_IMMUTABLE_STATUS");

        unitOfWork.MentorTeachingProposals.Remove(proposal);
        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<TeachingProposalValidationResponse>> ValidateDraftAsync(
        ValidateTeachingProposalRequest request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalValidationResponse>.Fail("Chỉ Giáo viên mới có thể kiểm tra đề xuất.", HttpStatusCode.Forbidden);

        request ??= new ValidateTeachingProposalRequest();
        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

        var snapshot = new ProposalCandidate(
            ProposalId: request.ProposalId,
            MentorId: claims.Id,
            CertificateLevelId: request.CertificateLevelId,
            ProgramType: NormalizeProgramType(request.ProgramType),
            CoursePlanId: request.CoursePlanId,
            CourseObjective: TrimOrNull(request.CourseObjective, 1500),
            ExpectedStartDate: request.ExpectedStartDate,
            CourseDurationWeeks: request.CourseDurationWeeks ?? policy.CourseDurationWeeks,
            SessionDurationMinutes: request.SessionDurationMinutes ?? policy.SessionMinutes,
            WeeklySessions: NormalizeWeeklySessions(request.WeeklySessions),
            OptionalAttachments: []);

        var validation = await EvaluateProposalAsync(snapshot, policy, nowUtc, ct);
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
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

        ApiResponse<TeachingProposalResponse>? failure = null;
        int? resultId = null;

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

            if (proposal.Status != MentorTeachingProposalStatus.Draft && proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    "Chỉ có thể gửi duyệt đề xuất đang ở trạng thái Nháp hoặc Cần bổ sung.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_IMMUTABLE_STATUS");
                return;
            }

            if (proposal.Revision != request.ExpectedRevision)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    "Bản nháp đã thay đổi trên phiên làm việc khác. Vui lòng đồng bộ bản mới nhất trước khi gửi duyệt.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_REVISION_CONFLICT");
                return;
            }

            var candidate = CandidateFromEntity(proposal);
            var validation = await EvaluateProposalAsync(candidate, policy, nowUtc, token);
            if (!validation.CanSubmit)
            {
                var firstErr = validation.Issues.FirstOrDefault(i => i.Severity == "Error");
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    firstErr?.Message ?? "Đề xuất chưa đáp ứng đủ quy định lịch dạy.",
                    HttpStatusCode.UnprocessableEntity,
                    firstErr?.Code ?? "PROPOSAL_VALIDATION_FAILED",
                    errors: validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message).ToList());
                return;
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
                ActorUserId = claims.Id,
                ActorRole = Role.Mentor.ToString(),
                Action = fromStatus == MentorTeachingProposalStatus.ChangesRequested ? "Resubmitted" : "Submitted",
                FromStatus = fromStatus,
                ToStatus = MentorTeachingProposalStatus.PendingReview,
                RevisionAfter = proposal.Revision,
                SnapshotJson = BuildSnapshotJson(proposal),
                CreatedAtUtc = nowUtc,
            }, token);

            var adminIds = await unitOfWork.UserAccounts.Query().AsNoTracking()
                .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(token);

            if (adminIds.Count > 0)
            {
                var mentorUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
                    .Where(u => u.Id == claims.Id)
                    .Select(u => new { u.FirstName, u.LastName })
                    .FirstOrDefaultAsync(token);
                var mentorName = mentorUser is null ? $"#{claims.Id}" : $"{mentorUser.FirstName} {mentorUser.LastName}".Trim();
                var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất #{proposal.Id}" : proposal.CourseTitle;

                var notifications = adminIds.Select(adminId => new
                {
                    UserId = adminId,
                    Title = "Có đề xuất mở lớp JLPT mới chờ duyệt",
                    Message = $"Giáo viên {mentorName} vừa gửi duyệt \"{titleLabel}\" ({candidate.WeeklySessions.Count} buổi/tuần × {proposal.CourseDurationWeeks} tuần).",
                    Type = "MentorSchedule",
                    LinkUrl = $"/admin/mentor-schedules?tab=proposals&proposalId={proposal.Id}",
                }).ToList();

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

            await unitOfWork.SaveChangesAsync(token);
            resultId = proposal.Id;
        }, ct);

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == resultId!.Value, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        var dto = MapProposal(loaded, policy, qualCodes, null, includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(claims.Id, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<TeachingProposalResponse>> WithdrawProposalAsync(
        int id, WithdrawTeachingProposalRequest? request, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể rút đề xuất.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        ApiResponse<TeachingProposalResponse>? failure = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.StudyCourses.LockScheduleAsync(claims.Id, claims.Id, token);

            var proposal = await unitOfWork.MentorTeachingProposals.Query()
                .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, token);
            if (proposal is null)
            {
                failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                return;
            }

            if (proposal.Status != MentorTeachingProposalStatus.PendingReview && proposal.Status != MentorTeachingProposalStatus.ChangesRequested)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    "Chỉ có thể rút đề xuất đang ở trạng thái Chờ duyệt hoặc Cần bổ sung.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_IMMUTABLE_STATUS");
                return;
            }

            if (request?.ExpectedRevision.HasValue == true && request.ExpectedRevision.Value != proposal.Revision)
            {
                failure = ApiResponse<TeachingProposalResponse>.Fail(
                    "Đề xuất vừa được cập nhật. Vui lòng tải lại trước khi rút.",
                    HttpStatusCode.Conflict,
                    "PROPOSAL_REVISION_CONFLICT");
                return;
            }

            var fromStatus = proposal.Status;
            proposal.Status = MentorTeachingProposalStatus.Withdrawn;
            proposal.Revision++;
            proposal.UpdatedAtUtc = nowUtc;
            unitOfWork.MentorTeachingProposals.Update(proposal);

            await unitOfWork.MentorTeachingProposalEvents.AddAsync(new MentorTeachingProposalEvent
            {
                ProposalId = proposal.Id,
                ActorUserId = claims.Id,
                ActorRole = Role.Mentor.ToString(),
                Action = "Withdrawn",
                FromStatus = fromStatus,
                ToStatus = MentorTeachingProposalStatus.Withdrawn,
                RevisionAfter = proposal.Revision,
                Reason = TrimOrNull(request?.Reason, 1000),
                CreatedAtUtc = nowUtc,
            }, token);

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Success(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    public async Task<ApiResponse<TeachingProposalResponse>> ReopenForRevisionAsync(int id, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Mentor)
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Giáo viên mới có thể mở bản chỉnh sửa.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

        var source = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.MentorId == claims.Id, ct);
        if (source is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất gốc.", "PROPOSAL_NOT_FOUND");

        if (source.Status != MentorTeachingProposalStatus.Rejected && source.Status != MentorTeachingProposalStatus.Withdrawn)
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Đề xuất đang chờ duyệt hoặc đã duyệt không thể nhân bản chỉnh sửa theo thao tác này.",
                HttpStatusCode.Conflict,
                "PROPOSAL_IMMUTABLE_STATUS");

        var clone = new MentorTeachingProposal
        {
            MentorId = claims.Id,
            CertificateLevelId = source.CertificateLevelId,
            ProgramType = source.ProgramType,
            CoursePlanId = source.CoursePlanId,
            CourseTitle = source.CourseTitle,
            CourseObjective = source.CourseObjective,
            ExpectedStartDate = source.ExpectedStartDate,
            CourseDurationWeeks = policy.CourseDurationWeeks,
            SessionDurationMinutes = policy.SessionMinutes,
            WeeklySessionsJson = source.WeeklySessionsJson,
            OptionalNotes = source.OptionalNotes,
            OptionalAttachmentsJson = source.OptionalAttachmentsJson,
            Status = MentorTeachingProposalStatus.Draft,
            Revision = 1,
            GroupPolicyId = policy.Id,
            PolicyVersionSnapshot = policy.Version,
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
            Reason = $"Tạo bản nháp mới từ đề xuất #{source.Id}",
            CreatedAtUtc = nowUtc,
        });

        await unitOfWork.MentorTeachingProposals.AddAsync(clone, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == clone.Id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(claims.Id, ct);
        return ApiResponse<TeachingProposalResponse>.Created(MapProposal(loaded, policy, qualCodes, null, includeHistory: true));
    }

    // ───────────────────────────── ADMIN ENDPOINTS ─────────────────────────────

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetAdminProposalsAsync(
        string? status = null, int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ Admin mới có quyền xem hàng đợi đề xuất mở lớp.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

        var query = QueryProposalsWithIncludes()
            .Where(p => p.Status != MentorTeachingProposalStatus.Draft);

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
                var check = await EvaluateProposalAsync(CandidateFromEntity(row), policy, nowUtc, ct);
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
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Admin mới có quyền xem chi tiết đề xuất này.", HttpStatusCode.Forbidden);

        var proposal = await QueryProposalsWithIncludes(includeEvents: true)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null)
            return ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(proposal.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([proposal.Id], ct);
        var dto = MapProposal(proposal, policy, qualCodes, linkedCohorts.GetValueOrDefault(proposal.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(proposal.MentorId, policy, nowUtc, ct);

        if (proposal.Status == MentorTeachingProposalStatus.PendingReview)
        {
            var check = await EvaluateProposalAsync(CandidateFromEntity(proposal), policy, nowUtc, ct);
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
            return ApiResponse<TeachingProposalResponse>.Fail("Chỉ Admin mới có quyền phê duyệt đề xuất mở lớp.", HttpStatusCode.Forbidden);

        if (request is null || string.IsNullOrWhiteSpace(request.Decision))
            return ApiResponse<TeachingProposalResponse>.Fail("Vui lòng chọn quyết định duyệt hợp lệ.");

        var decision = request.Decision.Trim();
        var isApprove = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isRequestChanges = string.Equals(decision, "RequestChanges", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isRequestChanges && !isReject)
            return ApiResponse<TeachingProposalResponse>.Fail("Quyết định phải là Approve, RequestChanges hoặc Reject.", HttpStatusCode.BadRequest, "INVALID_REVIEW_DECISION");

        var trimmedReason = TrimOrNull(request.Reason, 1500);
        if ((isRequestChanges || isReject) && string.IsNullOrWhiteSpace(trimmedReason))
            return ApiResponse<TeachingProposalResponse>.Fail(
                "Vui lòng nhập lý do rõ ràng khi từ chối hoặc yêu cầu Giáo viên chỉnh sửa đề xuất.",
                HttpStatusCode.BadRequest,
                "PROPOSAL_REVIEW_REASON_REQUIRED");

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);
        var adminUser = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Where(u => u.Id == claims.Id)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        var adminName = adminUser is null
            ? $"Admin #{claims.Id}"
            : $"{adminUser.FirstName} {adminUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(adminName)) adminName = adminUser?.Email ?? $"Admin #{claims.Id}";

        ApiResponse<TeachingProposalResponse>? failure = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var proposal = await unitOfWork.MentorTeachingProposals.Query()
                .FirstOrDefaultAsync(p => p.Id == id, token);
            if (proposal is null)
            {
                failure = ApiResponse<TeachingProposalResponse>.NotFound("Không tìm thấy đề xuất mở lớp.", "PROPOSAL_NOT_FOUND");
                return;
            }

            await unitOfWork.StudyCourses.LockScheduleAsync(proposal.MentorId, proposal.MentorId, token);

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

            if (isApprove)
            {
                var candidate = CandidateFromEntity(proposal);
                var validation = await EvaluateProposalAsync(candidate, policy, nowUtc, token);
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
                    ? JsonSerializer.Serialize(new { GeneratedAvailabilityIds = generatedSlotIds }, JsonOpts)
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
                }),
                Reason = trimmedReason,
                CreatedAtUtc = nowUtc,
            }, token);

            var titleLabel = string.IsNullOrWhiteSpace(proposal.CourseTitle) ? $"Đề xuất #{proposal.Id}" : proposal.CourseTitle;
            var notifTitle = isApprove
                ? "Đề xuất mở lớp JLPT đã được phê duyệt"
                : isRequestChanges
                    ? "Đề xuất mở lớp JLPT cần chỉnh sửa bổ sung"
                    : "Đề xuất mở lớp JLPT không được phê duyệt";
            var notifMessage = isApprove
                ? $"Admin đã phê duyệt \"{titleLabel}\" và ghi nhận {generatedSlotIds.Count} buổi dạy vào lịch sẵn sàng mở lớp."
                : isRequestChanges
                    ? $"Admin yêu cầu điều chỉnh \"{titleLabel}\": {trimmedReason}"
                    : $"Admin đã từ chối \"{titleLabel}\": {trimmedReason}";

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
                            LinkUrl = $"/mentor-shift-schedule?proposalId={proposal.Id}",
                        },
                    },
                }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc,
            });

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        if (failure is not null)
            return failure;

        var loaded = await QueryProposalsWithIncludes(includeEvents: true).FirstAsync(p => p.Id == id, ct);
        var qualCodes = await LoadMentorQualifiedLevelCodesAsync(loaded.MentorId, ct);
        var linkedCohorts = await LoadLinkedCohortsMapAsync([loaded.Id], ct);
        var dto = MapProposal(loaded, policy, qualCodes, linkedCohorts.GetValueOrDefault(loaded.Id), includeHistory: true);
        dto.MentorWorkload = await ComputeMentorWorkloadAsync(loaded.MentorId, policy, nowUtc, ct);
        return ApiResponse<TeachingProposalResponse>.Success(dto);
    }

    public async Task<ApiResponse<List<TeachingProposalResponse>>> GetApprovedProposalsForCohortAsync(
        int? mentorId = null, int? certificateLevelId = null, CancellationToken ct = default)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<List<TeachingProposalResponse>>.Fail("Chỉ Admin mới có quyền xem đề xuất đã duyệt.", HttpStatusCode.Forbidden);

        var nowUtc = DateTime.UtcNow;
        var policy = await groupPolicyService.GetEffectiveAsync(nowUtc, ct);

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
        CancellationToken ct)
    {
        var issues = new List<ProposalValidationIssue>();
        var allowedCounts = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv);
        var minPerWeek = allowedCounts.Min();
        var maxPerWeek = allowedCounts.Max();
        var restMinutes = Math.Max(0, policy.RestMinutes);
        var requiredSessionMinutes = policy.SessionMinutes;
        var requiredWeeks = policy.CourseDurationWeeks;

        // 1. Mentor verification & active check
        var mentor = await unitOfWork.UserAccounts.Query().AsNoTracking()
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Id == candidate.MentorId && u.Role == Role.Mentor && !u.IsDeleted, ct);
        if (mentor is null || mentor.MentorProfile?.VerificationStatus != MentorVerificationStatus.Approved)
        {
            issues.Add(Error("MENTOR_NOT_VERIFIED", "Hồ sơ Giáo viên cần được phê duyệt trước khi gửi đề xuất mở lớp."));
        }

        // 2. JLPT level existence check (Giáo viên tự chọn N5–N1; Admin xét duyệt năng lực khi duyệt đề xuất)
        if (!candidate.CertificateLevelId.HasValue || candidate.CertificateLevelId.Value <= 0)
        {
            issues.Add(Error("PROPOSAL_LEVEL_REQUIRED", "Vui lòng chọn trình độ JLPT dự kiến giảng dạy."));
        }
        else
        {
            var levelExists = await unitOfWork.CertificateLevels.Query().AsNoTracking()
                .AnyAsync(l => l.Id == candidate.CertificateLevelId.Value
                    && !l.IsDeleted
                    && l.CertificateType != null
                    && !l.CertificateType.IsDeleted
                    && l.CertificateType.IsActive, ct);
            if (!levelExists)
            {
                issues.Add(Error("PROPOSAL_LEVEL_INVALID", "Trình độ JLPT đã chọn không tồn tại hoặc không còn hoạt động."));
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
        else if (string.IsNullOrWhiteSpace(candidate.CourseObjective))
        {
            issues.Add(Error("PROPOSAL_OBJECTIVE_REQUIRED", "Vui lòng nhập mục tiêu khóa học khi không chọn gói khóa học có sẵn."));
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
                $"Mỗi đề xuất mở lớp cần tối thiểu {minPerWeek} buổi/tuần (hiện có {sessions.Count} buổi/tuần). Bạn có thể lưu nháp và bổ sung sau."));
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

        // 7. Expected start date & 10-week schedule expansion
        var occurrences = new List<GeneratedSessionOccurrenceResponse>();
        DateOnly? projectedEndDate = null;
        var todayVn = DateOnly.FromDateTime(LocalTime(nowUtc));

        if (!candidate.ExpectedStartDate.HasValue)
        {
            issues.Add(Error("PROPOSAL_START_DATE_REQUIRED", "Vui lòng chọn ngày khai giảng dự kiến."));
        }
        else if (candidate.ExpectedStartDate.Value < todayVn)
        {
            issues.Add(Error("PROPOSAL_START_DATE_PAST", "Ngày khai giảng dự kiến không được ở trong quá khứ."));
        }
        else if (sessions.Count > 0 && !issues.Any(i => i.Code is "PROPOSAL_INVALID_SESSION_SLOT" or "PROPOSAL_OWN_OVERLAP"))
        {
            occurrences = ExpandOccurrences(candidate.ExpectedStartDate.Value, sessions, candidate.CourseDurationWeeks, requiredSessionMinutes, nowUtc);
            if (occurrences.Count > 0)
            {
                projectedEndDate = DateOnly.ParseExact(occurrences[^1].LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (occurrences[0].StartUtc <= nowUtc)
                {
                    issues.Add(Error("PROPOSAL_START_DATE_PAST",
                        "Buổi học đầu tiên nằm trong quá khứ. Vui lòng chọn ngày khai giảng muộn hơn."));
                }
            }
        }

        // 8. Cross-schedule conflict & Mentor-wide workload check across full 10-week horizon
        if (occurrences.Count > 0)
        {
            var proposedIntervals = occurrences
                .Select(o => new ProposedTeachingInterval(o.StartUtc, o.EndUtc, o.SessionNumber))
                .ToList();

            // 8a. Authoritative cohort conflict check via MentorTeachingConflictService
            var cohortReport = await teachingConflictService.CheckAsync(
                candidate.MentorId,
                proposedIntervals,
                new TeachingConflictOptions
                {
                    RestMinutes = restMinutes,
                    DailyCap = policy.DailySessionCap,
                    WeeklyCap = policy.WeeklySessionCap,
                    IncludeOwnIntervals = false,
                },
                ct);

            foreach (var c in cohortReport.Conflicts)
            {
                var mappedCode = c.Code switch
                {
                    MentorTeachingConflictService.RestViolation => "PROPOSAL_REST_VIOLATION",
                    MentorTeachingConflictService.DailyLimit => "PROPOSAL_DAILY_CAP_EXCEEDED",
                    MentorTeachingConflictService.WeeklyLimit => "PROPOSAL_WEEKLY_CAP_EXCEEDED",
                    _ => "PROPOSAL_CONFLICT_APPROVED_SCHEDULE",
                };
                var localStart = LocalTime(c.ProposedStartUtc);
                issues.Add(new ProposalValidationIssue
                {
                    Code = mappedCode,
                    Severity = "Error",
                    Message = c.Message,
                    Weekday = (int)localStart.DayOfWeek,
                    StartMinute = localStart.Hour * 60 + localStart.Minute,
                    OccurrenceStartUtc = c.ProposedStartUtc,
                    OccurrenceEndUtc = c.ProposedEndUtc,
                    ConflictSource = c.CohortName ?? c.CommitmentKind,
                });
            }

            // 8b. Check against approved/pending non-deleted MentorAvailabilities + other PendingReview proposals
            var minUtc = occurrences[0].StartUtc.AddDays(-1);
            var maxUtc = occurrences[^1].EndUtc.AddDays(1);
            var weekWindowStart = WeekStartUtc(occurrences[0].StartUtc);
            var weekWindowEnd = WeekStartUtc(occurrences[^1].EndUtc).AddDays(7);
            if (weekWindowStart < minUtc) minUtc = weekWindowStart;
            if (weekWindowEnd > maxUtc) maxUtc = weekWindowEnd;

            // Các slot đã thuộc Cohort (để tránh đếm trùng với bước 8a)
            var cohortAvIds = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .Where(g => g.MentorId == candidate.MentorId && (g.Status == "Published" || g.Status == "Draft"))
                .SelectMany(g => g.Sessions.Select(s => s.AvailabilityId))
                .ToListAsync(ct);
            var cohortAvSet = cohortAvIds.ToHashSet();

            var existingAvailabilities = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
                .Where(a => a.MentorId == candidate.MentorId
                    && !a.IsDeleted
                    && a.RejectedAt == null
                    && a.EndTime > minUtc
                    && a.StartTime < maxUtc)
                .Select(a => new { a.Id, a.StartTime, a.EndTime, a.IsApproved, a.TeachingProposalId })
                .ToListAsync(ct);

            // Other PendingReview proposals of the same Mentor (allowing multiple concurrent proposals when non-conflicting!)
            var otherPendingProposals = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                .Where(p => p.MentorId == candidate.MentorId
                    && p.Status == MentorTeachingProposalStatus.PendingReview
                    && (!candidate.ProposalId.HasValue || p.Id != candidate.ProposalId.Value))
                .ToListAsync(ct);

            // Duplicate proposal detection across PendingReview & Approved
            var signature = BuildDuplicateSignature(candidate.CertificateLevelId, candidate.CoursePlanId, candidate.ExpectedStartDate, sessions);
            var otherActiveOrPending = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
                .Where(p => p.MentorId == candidate.MentorId
                    && (p.Status == MentorTeachingProposalStatus.PendingReview || p.Status == MentorTeachingProposalStatus.Approved)
                    && (!candidate.ProposalId.HasValue || p.Id != candidate.ProposalId.Value))
                .Select(p => new { p.Id, p.CertificateLevelId, p.CoursePlanId, p.ExpectedStartDate, p.WeeklySessionsJson, p.CourseTitle })
                .ToListAsync(ct);

            var duplicate = otherActiveOrPending.FirstOrDefault(p =>
                BuildDuplicateSignature(p.CertificateLevelId, p.CoursePlanId, p.ExpectedStartDate, DeserializeSessions(p.WeeklySessionsJson)) == signature);
            if (duplicate is not null)
            {
                issues.Add(Error("PROPOSAL_DUPLICATE_DETECTED",
                    $"Bạn đã có đề xuất tương tự (#{duplicate.Id}{(string.IsNullOrWhiteSpace(duplicate.CourseTitle) ? "" : $" – {duplicate.CourseTitle}")}) cùng trình độ, ngày khai giảng và lịch tuần này."));
            }

            var combinedExternal = new List<(DateTime StartUtc, DateTime EndUtc, string Source, bool IsPendingProposal)>();
            foreach (var av in existingAvailabilities)
            {
                if (cohortAvSet.Contains(av.Id)) continue; // Đã kiểm tra overlap ở bước 8a, vẫn cần tính workload bên dưới
                combinedExternal.Add((
                    MentorTeachingConflictService.Utc(av.StartTime),
                    MentorTeachingConflictService.Utc(av.EndTime),
                    av.IsApproved ? "Lịch dạy đã duyệt" : "Khung giờ đang chờ duyệt",
                    false));
            }

            foreach (var other in otherPendingProposals)
            {
                if (!other.ExpectedStartDate.HasValue) continue;
                var otherSessions = DeserializeSessions(other.WeeklySessionsJson);
                if (otherSessions.Count == 0) continue;
                var otherOccs = ExpandOccurrences(other.ExpectedStartDate.Value, otherSessions, other.CourseDurationWeeks, other.SessionDurationMinutes, DateTime.MinValue);
                var label = string.IsNullOrWhiteSpace(other.CourseTitle) ? $"Đề xuất chờ duyệt #{other.Id}" : $"Đề xuất chờ duyệt \"{other.CourseTitle}\"";
                foreach (var o in otherOccs)
                {
                    if (o.EndUtc <= minUtc || o.StartUtc >= maxUtc) continue;
                    combinedExternal.Add((o.StartUtc, o.EndUtc, label, true));
                }
            }

            // Overlap & Rest against non-cohort availabilities and other pending proposals
            var conflictAdded = 0;
            foreach (var occ in occurrences)
            {
                foreach (var ext in combinedExternal)
                {
                    if (conflictAdded >= 20) break;
                    if (ext.StartUtc < occ.EndUtc && occ.StartUtc < ext.EndUtc)
                    {
                        issues.Add(new ProposalValidationIssue
                        {
                            Code = ext.IsPendingProposal ? "PROPOSAL_CONFLICT_PENDING_PROPOSAL" : "PROPOSAL_CONFLICT_APPROVED_SCHEDULE",
                            Severity = "Error",
                            Message = $"Buổi {occ.LocalDate} ({occ.LocalStart}–{occ.LocalEnd}) bị trùng giờ với {ext.Source}.",
                            Weekday = occ.Weekday,
                            StartMinute = ParseClockToMinute(occ.LocalStart),
                            OccurrenceStartUtc = occ.StartUtc,
                            OccurrenceEndUtc = occ.EndUtc,
                            ConflictSource = ext.Source,
                        });
                        conflictAdded++;
                    }
                    else
                    {
                        var gap = occ.StartUtc >= ext.EndUtc ? occ.StartUtc - ext.EndUtc : ext.StartUtc - occ.EndUtc;
                        if (gap < TimeSpan.FromMinutes(restMinutes))
                        {
                            issues.Add(new ProposalValidationIssue
                            {
                                Code = "PROPOSAL_REST_VIOLATION",
                                Severity = "Error",
                                Message = $"Buổi {occ.LocalDate} ({occ.LocalStart}–{occ.LocalEnd}) quá sát {ext.Source}; cần nghỉ tối thiểu {restMinutes} phút giữa hai buổi.",
                                Weekday = occ.Weekday,
                                StartMinute = ParseClockToMinute(occ.LocalStart),
                                OccurrenceStartUtc = occ.StartUtc,
                                OccurrenceEndUtc = occ.EndUtc,
                                ConflictSource = ext.Source,
                            });
                            conflictAdded++;
                        }
                    }
                }
            }

            // Combined Mentor-wide Daily & Weekly caps across [approved availabilities (incl. cohorts) + other pending proposals + this proposal]
            var allExistingForCap = existingAvailabilities
                .Select(a => MentorTeachingConflictService.Utc(a.StartTime))
                .Concat(combinedExternal.Where(e => e.IsPendingProposal).Select(e => e.StartUtc))
                .ToList();

            if (policy.DailySessionCap > 0)
            {
                var existingByDay = allExistingForCap.GroupBy(u => DateOnly.FromDateTime(LocalTime(u)))
                    .ToDictionary(g => g.Key, g => g.Count());
                foreach (var dayGroup in occurrences.GroupBy(o => DateOnly.FromDateTime(LocalTime(o.StartUtc))))
                {
                    var total = dayGroup.Count() + existingByDay.GetValueOrDefault(dayGroup.Key);
                    if (total > policy.DailySessionCap && !issues.Any(i => i.Code == "PROPOSAL_DAILY_CAP_EXCEEDED" && i.OccurrenceStartUtc.HasValue && DateOnly.FromDateTime(LocalTime(i.OccurrenceStartUtc.Value)) == dayGroup.Key))
                    {
                        var first = dayGroup.First();
                        issues.Add(new ProposalValidationIssue
                        {
                            Code = "PROPOSAL_DAILY_CAP_EXCEEDED",
                            Severity = "Error",
                            Message = $"Tổng lịch dạy ngày {dayGroup.Key:dd/MM/yyyy} đạt {total} buổi (bao gồm lịch đã duyệt và đề xuất chờ duyệt khác), vượt giới hạn {policy.DailySessionCap} buổi/ngày theo chính sách.",
                            Weekday = first.Weekday,
                            StartMinute = ParseClockToMinute(first.LocalStart),
                            OccurrenceStartUtc = first.StartUtc,
                            OccurrenceEndUtc = first.EndUtc,
                        });
                    }
                }
            }

            if (policy.WeeklySessionCap > 0)
            {
                var existingByWeek = allExistingForCap.GroupBy(u => LocalWeekStart(DateOnly.FromDateTime(LocalTime(u))))
                    .ToDictionary(g => g.Key, g => g.Count());
                foreach (var weekGroup in occurrences.GroupBy(o => LocalWeekStart(DateOnly.FromDateTime(LocalTime(o.StartUtc)))))
                {
                    var total = weekGroup.Count() + existingByWeek.GetValueOrDefault(weekGroup.Key);
                    if (total > policy.WeeklySessionCap && !issues.Any(i => i.Code == "PROPOSAL_WEEKLY_CAP_EXCEEDED" && i.OccurrenceStartUtc.HasValue && LocalWeekStart(DateOnly.FromDateTime(LocalTime(i.OccurrenceStartUtc.Value))) == weekGroup.Key))
                    {
                        var first = weekGroup.First();
                        issues.Add(new ProposalValidationIssue
                        {
                            Code = "PROPOSAL_WEEKLY_CAP_EXCEEDED",
                            Severity = "Error",
                            Message = $"Tổng lịch dạy tuần bắt đầu {weekGroup.Key:dd/MM/yyyy} đạt {total} buổi, vượt giới hạn {policy.WeeklySessionCap} buổi/tuần theo chính sách.",
                            Weekday = first.Weekday,
                            StartMinute = ParseClockToMinute(first.LocalStart),
                            OccurrenceStartUtc = first.StartUtc,
                            OccurrenceEndUtc = first.EndUtc,
                        });
                    }
                }
            }
        }

        var workload = await ComputeMentorWorkloadAsync(candidate.MentorId, policy, nowUtc, ct);
        var hasErrors = issues.Any(i => i.Severity == "Error");
        var totalSessions = occurrences.Count > 0 ? occurrences.Count : sessions.Count * candidate.CourseDurationWeeks;

        return new TeachingProposalValidationResponse
        {
            IsValid = !hasErrors,
            CanSubmit = !hasErrors && sessions.Count >= minPerWeek && sessions.Count <= maxPerWeek && occurrences.Count > 0,
            WeeklySessionCount = sessions.Count,
            TotalSessions = totalSessions,
            TotalTeachingMinutes = totalSessions * candidate.SessionDurationMinutes,
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
            .Select(c => new { c.Id, c.Plan.SessionsPerWeek })
            .ToListAsync(ct);

        var proposals = await unitOfWork.MentorTeachingProposals.Query().AsNoTracking()
            .Where(p => p.MentorId == mentorId)
            .Select(p => new { p.Status, p.WeeklySessionsJson })
            .ToListAsync(ct);

        var pendingProps = proposals.Where(p => p.Status == MentorTeachingProposalStatus.PendingReview).ToList();
        var pendingWeeklySessions = pendingProps.Sum(p => DeserializeSessions(p.WeeklySessionsJson).Count);
        var approvedCount = proposals.Count(p => p.Status == MentorTeachingProposalStatus.Approved);
        var draftCount = proposals.Count(p => p.Status is MentorTeachingProposalStatus.Draft or MentorTeachingProposalStatus.ChangesRequested);

        var weekStart = WeekStartUtc(nowUtc);
        var horizonEnd = weekStart.AddDays(70);
        var upcomingSlots = await unitOfWork.MentorAvailabilities.Query().AsNoTracking()
            .Where(a => a.MentorId == mentorId && !a.IsDeleted && a.RejectedAt == null && a.IsApproved
                && a.StartTime >= weekStart && a.StartTime < horizonEnd)
            .Select(a => a.StartTime)
            .ToListAsync(ct);

        var peakDay = upcomingSlots
            .GroupBy(t => DateOnly.FromDateTime(LocalTime(MentorTeachingConflictService.Utc(t))))
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .Max();

        var weeklyCommitted = activeCohorts.Sum(c => c.SessionsPerWeek);

        return new MentorWorkloadSummaryResponse
        {
            ActiveCohortsCount = activeCohorts.Count,
            PendingProposalsCount = pendingProps.Count,
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

    private async Task<Dictionary<int, (int Id, string Name, string Status)>> LoadLinkedCohortsMapAsync(List<int> proposalIds, CancellationToken ct)
    {
        if (proposalIds.Count == 0) return [];
        var rows = await unitOfWork.StudyCohorts.Query().AsNoTracking()
            .Where(c => c.SourceProposalId.HasValue && proposalIds.Contains(c.SourceProposalId.Value))
            .OrderByDescending(c => c.Id)
            .Select(c => new { ProposalId = c.SourceProposalId!.Value, c.Id, c.Name, c.Status })
            .ToListAsync(ct);

        var dict = new Dictionary<int, (int, string, string)>();
        foreach (var r in rows)
            dict.TryAdd(r.ProposalId, (r.Id, r.Name, r.Status));
        return dict;
    }

    private static TeachingProposalResponse MapProposal(
        MentorTeachingProposal p,
        MentorGroupPolicy policy,
        List<string> mentorQualifiedLevels,
        (int Id, string Name, string Status)? linkedCohort,
        bool includeHistory)
    {
        var sessions = DeserializeSessions(p.WeeklySessionsJson);
        var attachments = DeserializeAttachments(p.OptionalAttachmentsJson);
        var occurrences = p.ExpectedStartDate.HasValue && sessions.Count > 0
            ? ExpandOccurrences(p.ExpectedStartDate.Value, sessions, p.CourseDurationWeeks, p.SessionDurationMinutes, DateTime.MinValue)
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
            CourseDurationWeeks = p.CourseDurationWeeks > 0 ? p.CourseDurationWeeks : policy.CourseDurationWeeks,
            SessionDurationMinutes = p.SessionDurationMinutes > 0 ? p.SessionDurationMinutes : policy.SessionMinutes,
            WeeklySessionCount = sessions.Count,
            TotalSessions = sessions.Count * (p.CourseDurationWeeks > 0 ? p.CourseDurationWeeks : policy.CourseDurationWeeks),
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
        normalizedSessions = NormalizeWeeklySessions(request.WeeklySessions);
        normalizedAttachments = [];

        var maxPerWeek = ParseAllowedSessionsPerWeek(policy.AllowedSessionsPerWeekCsv).Max();
        if (normalizedSessions.Count > maxPerWeek)
        {
            return ("PROPOSAL_MAX_SESSIONS_EXCEEDED",
                $"Mỗi đề xuất mở lớp chỉ được chọn tối đa {maxPerWeek} buổi/tuần.");
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
                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                    || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
                {
                    return ("PROPOSAL_INVALID_ATTACHMENT", "Đường dẫn tệp đính kèm phải sử dụng giao thức http/https hợp lệ.");
                }

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

    private static List<GeneratedSessionOccurrenceResponse> ExpandOccurrences(
        DateOnly expectedStartDate,
        List<WeeklySessionSlotDto> weeklySessions,
        int durationWeeks,
        int sessionMinutes,
        DateTime nowUtc)
    {
        if (weeklySessions.Count == 0 || durationWeeks <= 0) return [];

        var sorted = weeklySessions
            .OrderBy(s => ((s.Weekday + 6) % 7))
            .ThenBy(s => s.StartMinute)
            .ToList();

        // Tìm thứ 2 đầu tiên sao cho tất cả các buổi tuần 1 đều >= expectedStartDate (và > nowUtc khi kiểm tra thực tế)
        // Nhờ đó đảm bảo trọn vẹn `durationWeeks` tuần ISO liên tiếp khớp StudyCohortService.EnsureWholeCourseSchedule.
        var startWeekMonday = LocalWeekStart(expectedStartDate);
        var fitsFirstWeek = sorted.All(s =>
        {
            var offset = (s.Weekday + 6) % 7;
            var date = startWeekMonday.AddDays(offset);
            if (date < expectedStartDate) return false;
            if (nowUtc == DateTime.MinValue) return true;
            var utcStart = LocalDateMinuteToUtc(date, s.StartMinute);
            return utcStart > nowUtc;
        });

        var firstWeekMonday = fitsFirstWeek ? startWeekMonday : startWeekMonday.AddDays(7);
        var result = new List<GeneratedSessionOccurrenceResponse>(durationWeeks * sorted.Count);
        var sessionNum = 0;

        for (var week = 0; week < durationWeeks; week++)
        {
            var weekMon = firstWeekMonday.AddDays(week * 7);
            foreach (var slot in sorted)
            {
                var offset = (slot.Weekday + 6) % 7;
                var localDate = weekMon.AddDays(offset);
                var startUtc = LocalDateMinuteToUtc(localDate, slot.StartMinute);
                var endUtc = startUtc.AddMinutes(sessionMinutes);
                sessionNum++;

                result.Add(new GeneratedSessionOccurrenceResponse
                {
                    SessionNumber = sessionNum,
                    WeekNumber = week + 1,
                    Weekday = slot.Weekday,
                    LocalDate = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    LocalStart = Clock(slot.StartMinute),
                    LocalEnd = Clock(slot.StartMinute + sessionMinutes),
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                });
            }
        }

        return result;
    }

    private static List<WeeklySessionSlotDto> NormalizeWeeklySessions(List<WeeklySessionSlotDto>? raw)
    {
        if (raw is null || raw.Count == 0) return [];
        return raw
            .Where(s => s is not null)
            .Select(s => new WeeklySessionSlotDto
            {
                Weekday = s.Weekday,
                StartMinute = s.StartMinute,
                EndMinute = s.EndMinute > 0 ? s.EndMinute : s.StartMinute + 90,
            })
            .OrderBy(s => (s.Weekday + 6) % 7)
            .ThenBy(s => s.StartMinute)
            .ToList();
    }

    private static List<WeeklySessionSlotDto> DeserializeSessions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return NormalizeWeeklySessions(JsonSerializer.Deserialize<List<WeeklySessionSlotDto>>(json, JsonOpts));
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
            .Where(n => n is >= 1 and <= 7)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
        return parsed.Count > 0 ? parsed : [2, 3, 4];
    }

    private static string BuildDuplicateSignature(int? levelId, int? planId, DateOnly? startDate, List<WeeklySessionSlotDto> sessions)
    {
        var slotPart = string.Join("|", sessions
            .OrderBy(s => (s.Weekday + 6) % 7)
            .ThenBy(s => s.StartMinute)
            .Select(s => $"{s.Weekday}:{s.StartMinute}-{s.EndMinute}"));
        return $"{levelId ?? 0}:{planId ?? 0}:{startDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""}:{slotPart}";
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

    private static int ParseClockToMinute(string hhmm)
    {
        var parts = hhmm.Split(':');
        if (parts.Length != 2) return 0;
        return int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 0;
    }

    private static string WeekdayLabel(int weekday)
        => weekday == 0 ? "Chủ nhật" : $"Thứ {weekday + 1}";
}
