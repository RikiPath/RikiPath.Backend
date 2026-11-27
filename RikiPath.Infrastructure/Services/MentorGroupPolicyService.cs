using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorGroupPolicyService(AppDbContext context, AppSettings? settings = null) : IMentorGroupPolicyService
{
    public Task<MentorGroupPolicy?> GetEffectiveAsync(
        DateTime sessionStartUtc, CancellationToken cancellationToken = default)
        => context.MentorGroupPolicies.AsNoTracking()
            .Include(x => x.LevelLimits)
                .ThenInclude(l => l.CertificateLevel)
            .Include(x => x.CreatedByAdmin)
            .Include(x => x.LastAppliedByAdmin)
            .Include(x => x.UpdatedByAdmin)
            .Where(x => x.IsActive && !x.IsArchivedRevision && !x.IsDeleted)
            .OrderByDescending(x => x.LastAppliedAtUtc)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MentorGroupPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var liveRows = await context.MentorGroupPolicies.AsNoTracking()
            .Include(x => x.LevelLimits)
                .ThenInclude(l => l.CertificateLevel)
            .Include(x => x.CreatedByAdmin)
            .Include(x => x.LastAppliedByAdmin)
            .Include(x => x.UpdatedByAdmin)
            .Where(x => !x.IsArchivedRevision && !x.IsDeleted)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        if (liveRows.Count == 0)
            return liveRows;

        var liveVersions = liveRows.Select(r => r.Version).Distinct().ToList();
        var versionIdRows = await context.MentorGroupPolicies.AsNoTracking()
            .Where(p => liveVersions.Contains(p.Version))
            .Select(p => new { p.Id, p.Version })
            .ToListAsync(cancellationToken);

        var allPolicyIds = versionIdRows.Select(x => x.Id).Distinct().ToList();
        var referencedIds = await GetReferencedPolicyIdSetAsync(allPolicyIds, cancellationToken);
        var referencedVersions = versionIdRows
            .Where(x => referencedIds.Contains(x.Id))
            .Select(x => x.Version)
            .ToHashSet();

        foreach (var row in liveRows)
            ApplyDeleteEligibility(row, referencedVersions.Contains(row.Version));

        return liveRows;
    }

    public async Task<IReadOnlyList<SupportedPolicyLevelDto>> GetSupportedLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.CertificateLevels.AsNoTracking()
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .Select(l => new
            {
                l.Id,
                l.Code,
                TypeName = l.CertificateType != null ? l.CertificateType.Name : null,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(l => MentorSlotPolicy.IsSupportedClassLevel(l.Code))
            .Select(l =>
            {
                var code = l.Code!.Trim().ToUpperInvariant();
                var displayName = !string.IsNullOrWhiteSpace(l.TypeName)
                    ? $"{l.TypeName.Trim()} {code}".Trim()
                    : code;
                return new SupportedPolicyLevelDto(l.Id, code, displayName);
            })
            .ToList();
    }

    public async Task<ApiResponse<MentorGroupPolicy>> CreateVersionAsync(
        CreateMentorGroupPolicyRequest request,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var supportedLevelRows = await GetSupportedLevelsAsync(cancellationToken);
        var supportedCodes = supportedLevelRows
            .Select(l => l.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var error = Validate(request, adminId, supportedCodes, requireExpectedCurrentVersion: true);
        // Quyết định chính thức của Product Owner: không chuyển lớp sau khi khóa học bắt đầu. Chỉ chặn phiên bản MỚI;
        // các phiên bản đã lưu giữ nguyên (kể cả khi có cấu hình phê duyệt cũ).
        if (error is null && request.TransferAllowedAfterSourceStart)
            error = "Không thể bật chuyển lớp sau khi lớp bắt đầu: học viên không được chuyển lớp sau khi khóa học đã bắt đầu.";
        if (error is not null)
            return ApiResponse<MentorGroupPolicy>.Fail(error, HttpStatusCode.BadRequest);

        try
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;

                var latest = await context.MentorGroupPolicies
                    .Include(x => x.LevelLimits)
                        .ThenInclude(l => l.CertificateLevel)
                    .Where(x => !x.IsArchivedRevision && !x.IsDeleted)
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latest is null || latest.Version != request.ExpectedCurrentVersion)
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Phiên bản chính sách đã thay đổi. Vui lòng tải lại trước khi lưu.",
                        HttpStatusCode.Conflict);

                var maxAllVersion = await context.MentorGroupPolicies
                    .MaxAsync(x => (int?)x.Version, cancellationToken) ?? latest.Version;

                var baseline = request.SourceVersion is int sourceVer && sourceVer > 0 && sourceVer != latest.Version
                    ? await context.MentorGroupPolicies
                        .Include(x => x.LevelLimits)
                            .ThenInclude(l => l.CertificateLevel)
                        .Where(x => !x.IsArchivedRevision && !x.IsDeleted && x.Version == sourceVer)
                        .FirstOrDefaultAsync(cancellationToken) ?? latest
                    : latest;

                var nowUtc = DateTime.UtcNow;
                var patternsJson = ResolvePatternsJson(request.RecurringWeekdayPatterns, baseline.RecurringWeekdayPatternsJson);

                var currentlyActiveRows = await context.MentorGroupPolicies
                    .Where(x => x.IsActive && !x.IsArchivedRevision && !x.IsDeleted)
                    .ToListAsync(cancellationToken);
                var previousActive = currentlyActiveRows.FirstOrDefault();

                if (request.ApplyImmediately && currentlyActiveRows.Count > 0)
                {
                    foreach (var activeRow in currentlyActiveRows)
                        activeRow.IsActive = false;
                    await context.SaveChangesAsync(cancellationToken);
                }

                var policy = new MentorGroupPolicy
                {
                    Version = maxAllVersion + 1,
                    Revision = 1,
                    IsActive = request.ApplyImmediately,
                    IsArchivedRevision = false,
                    IsDeleted = false,
                    EffectiveFromUtc = nowUtc,
                    LastAppliedAtUtc = request.ApplyImmediately ? nowUtc : null,
                    LastAppliedByAdminId = request.ApplyImmediately ? adminId : null,
                    CreatedByAdminId = adminId,
                    CreatedAtUtc = nowUtc,
                };
                ApplyPolicyFields(policy, request, patternsJson);

                var (beforeLimits, afterLimits) = PopulateLevelLimits(policy, baseline, request.MaxActiveClassesByLevel, supportedLevelRows);
                if (afterLimits.Count > 0 && afterLimits.Values.All(v => v <= 0))
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Phải cho phép mở lớp ở ít nhất một cấp độ JLPT (giá trị >= 1).",
                        HttpStatusCode.BadRequest);

                context.MentorGroupPolicies.Add(policy);
                context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    ActorUserId = adminId,
                    EntityType = "MentorGroupPolicy",
                    EntityId = policy.Version,
                    Action = request.ApplyImmediately ? "PublishedAndAppliedVersion" : "PublishedVersion",
                    BeforeJson = JsonSerializer.Serialize(BuildAuditSnapshot(baseline, beforeLimits, previousActive?.Version)),
                    AfterJson = JsonSerializer.Serialize(BuildAuditSnapshot(policy, afterLimits, policy.IsActive ? policy.Version : previousActive?.Version)),
                    CreatedAtUtc = nowUtc,
                });

                await context.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);

                var persisted = await LoadDetailedByIdAsync(policy.Id, cancellationToken);
                ApplyDeleteEligibility(persisted, isReferenced: false);
                return ApiResponse<MentorGroupPolicy>.Created(persisted);
            });
        }
        catch (DbUpdateException)
        {
            return ApiResponse<MentorGroupPolicy>.Fail(
                "Phiên bản chính sách đã được tạo bởi yêu cầu khác. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }
    }

    public async Task<ApiResponse<MentorGroupPolicy>> UpdateVersionAsync(
        int policyId,
        UpdateMentorGroupPolicyRequest request,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (policyId <= 0)
            return ApiResponse<MentorGroupPolicy>.Fail("Phiên bản chính sách không hợp lệ.", HttpStatusCode.BadRequest);
        if (request.TransferAllowedAfterSourceStart)
            return ApiResponse<MentorGroupPolicy>.Fail(
                "Không thể bật chuyển lớp sau khi lớp bắt đầu: học viên không được chuyển lớp sau khi khóa học đã bắt đầu.",
                HttpStatusCode.BadRequest);

        return await SaveUpdatedPolicyAsync(
            policyId,
            adminId,
            request.ExpectedRevision,
            "UpdatedVersion",
            target => new CreateMentorGroupPolicyRequest
            {
                ExpectedCurrentVersion = target.Version,
                MaxLearners = request.MaxLearners,
                MinLearnersToStart = request.MinLearnersToStart,
                MaxTotalActiveClassesPerMentor = request.MaxTotalActiveClassesPerMentor,
                MaxActiveClassesByLevel = request.MaxActiveClassesByLevel,
                CourseDurationWeeks = request.CourseDurationWeeks,
                AllowedSessionsPerWeekCsv = request.AllowedSessionsPerWeekCsv,
                DefaultSessionsPerWeek = request.DefaultSessionsPerWeek,
                SessionMinutes = request.SessionMinutes,
                RestMinutes = request.RestMinutes,
                DailySessionCap = request.DailySessionCap,
                WeeklySessionCap = request.WeeklySessionCap,
                PublicationHorizonDays = request.PublicationHorizonDays,
                MinimumProposalLeadDays = request.MinimumProposalLeadDays,
                MinimumPublishedEnrollmentDays = request.MinimumPublishedEnrollmentDays,
                EnrollmentCutoffMinutesBeforeFirstSession = request.EnrollmentCutoffMinutesBeforeFirstSession,
                CheckoutHoldMinutes = request.CheckoutHoldMinutes,
                CancellationLeadHours = request.CancellationLeadHours,
                ReschedulingLeadHours = request.ReschedulingLeadHours,
                LearnerTransferEnabled = request.LearnerTransferEnabled,
                TransferAllowedAfterSourceStart = false,
                MentorReplacementEnabled = request.MentorReplacementEnabled,
                MentorReplacementLeadHours = request.MentorReplacementLeadHours,
                HomeworkDeadlineDays = request.HomeworkDeadlineDays,
                PlacementAttemptLimit = request.PlacementAttemptLimit,
                RemediationCompletionThresholdPercent = request.RemediationCompletionThresholdPercent,
                RemediationThresholdComparison = request.RemediationThresholdComparison,
                RequireOfficialExamEvidence = target.RequireOfficialExamEvidence,
                RequireAdminRemediationReview = target.RequireAdminRemediationReview,
                KpiReportingPeriodDays = request.KpiReportingPeriodDays,
                TuitionPerSessionN5 = request.TuitionPerSessionN5 ?? target.TuitionPerSessionN5,
                TuitionPerSessionN4 = request.TuitionPerSessionN4 ?? target.TuitionPerSessionN4,
                TuitionPerSessionN3 = request.TuitionPerSessionN3 ?? target.TuitionPerSessionN3,
                TuitionPerSessionN2 = request.TuitionPerSessionN2 ?? target.TuitionPerSessionN2,
                RecurringWeekdayPatterns = DeserializePatterns(target.RecurringWeekdayPatternsJson),
            },
            cancellationToken);
    }

    public async Task<ApiResponse<MentorGroupPolicy>> UpdateSectionAsync(
        int policyId,
        string sectionKey,
        UpdateMentorGroupPolicySectionRequest request,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (policyId <= 0)
            return ApiResponse<MentorGroupPolicy>.Fail("Phiên bản chính sách không hợp lệ.", HttpStatusCode.BadRequest);

        var normalizedSection = (sectionKey ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedSection is not ("limits" or "structure" or "schedule" or "admission" or "coordination" or "training"))
            return ApiResponse<MentorGroupPolicy>.Fail("Nhóm cấu hình không hợp lệ.", HttpStatusCode.BadRequest);

        return await SaveUpdatedPolicyAsync(
            policyId,
            adminId,
            request.ExpectedRevision,
            $"UpdatedSection:{normalizedSection}",
            target =>
            {
                var currentLimits = target.GetMaxActiveClassesByLevelCode();
                return new CreateMentorGroupPolicyRequest
                {
                    ExpectedCurrentVersion = target.Version,
                    MaxTotalActiveClassesPerMentor = normalizedSection == "limits"
                        ? (request.MaxTotalActiveClassesPerMentor ?? target.MaxTotalActiveClassesPerMentor)
                        : target.MaxTotalActiveClassesPerMentor,
                    MaxActiveClassesByLevel = normalizedSection == "limits" && request.MaxActiveClassesByLevel is not null
                        ? request.MaxActiveClassesByLevel
                        : currentLimits,
                    MinLearnersToStart = normalizedSection == "structure"
                        ? (request.MinLearnersToStart ?? target.MinLearnersToStart)
                        : target.MinLearnersToStart,
                    MaxLearners = normalizedSection == "structure"
                        ? (request.MaxLearners ?? target.MaxLearners)
                        : target.MaxLearners,
                    CourseDurationWeeks = normalizedSection == "structure"
                        ? (request.CourseDurationWeeks ?? target.CourseDurationWeeks)
                        : target.CourseDurationWeeks,
                    AllowedSessionsPerWeekCsv = normalizedSection == "structure"
                        ? (request.AllowedSessionsPerWeekCsv ?? target.AllowedSessionsPerWeekCsv)
                        : target.AllowedSessionsPerWeekCsv,
                    DefaultSessionsPerWeek = normalizedSection == "structure"
                        ? (request.DefaultSessionsPerWeek ?? target.DefaultSessionsPerWeek)
                        : target.DefaultSessionsPerWeek,
                    SessionMinutes = normalizedSection == "structure"
                        ? (request.SessionMinutes ?? target.SessionMinutes)
                        : target.SessionMinutes,
                    RestMinutes = normalizedSection == "schedule"
                        ? (request.RestMinutes ?? target.RestMinutes)
                        : target.RestMinutes,
                    DailySessionCap = normalizedSection == "schedule"
                        ? (request.DailySessionCap ?? target.DailySessionCap)
                        : target.DailySessionCap,
                    WeeklySessionCap = normalizedSection == "schedule"
                        ? (request.WeeklySessionCap ?? target.WeeklySessionCap)
                        : target.WeeklySessionCap,
                    PublicationHorizonDays = normalizedSection == "schedule"
                        ? (request.PublicationHorizonDays ?? target.PublicationHorizonDays)
                        : target.PublicationHorizonDays,
                    MinimumProposalLeadDays = normalizedSection == "admission"
                        ? (request.MinimumProposalLeadDays ?? target.MinimumProposalLeadDays)
                        : target.MinimumProposalLeadDays,
                    MinimumPublishedEnrollmentDays = normalizedSection == "admission"
                        ? (request.MinimumPublishedEnrollmentDays ?? target.MinimumPublishedEnrollmentDays)
                        : target.MinimumPublishedEnrollmentDays,
                    EnrollmentCutoffMinutesBeforeFirstSession = normalizedSection == "admission"
                        ? (request.EnrollmentCutoffMinutesBeforeFirstSession ?? target.EnrollmentCutoffMinutesBeforeFirstSession)
                        : target.EnrollmentCutoffMinutesBeforeFirstSession,
                    CheckoutHoldMinutes = normalizedSection == "admission"
                        ? (request.CheckoutHoldMinutes ?? target.CheckoutHoldMinutes)
                        : target.CheckoutHoldMinutes,
                    CancellationLeadHours = normalizedSection == "coordination"
                        ? (request.CancellationLeadHours ?? target.CancellationLeadHours)
                        : target.CancellationLeadHours,
                    ReschedulingLeadHours = normalizedSection == "coordination"
                        ? (request.ReschedulingLeadHours ?? target.ReschedulingLeadHours)
                        : target.ReschedulingLeadHours,
                    MentorReplacementLeadHours = normalizedSection == "coordination"
                        ? (request.MentorReplacementLeadHours ?? target.MentorReplacementLeadHours)
                        : target.MentorReplacementLeadHours,
                    LearnerTransferEnabled = normalizedSection == "coordination"
                        ? (request.LearnerTransferEnabled ?? target.LearnerTransferEnabled)
                        : target.LearnerTransferEnabled,
                    TransferAllowedAfterSourceStart = false,
                    MentorReplacementEnabled = normalizedSection == "coordination"
                        ? (request.MentorReplacementEnabled ?? target.MentorReplacementEnabled)
                        : target.MentorReplacementEnabled,
                    HomeworkDeadlineDays = normalizedSection == "training"
                        ? (request.HomeworkDeadlineDays ?? target.HomeworkDeadlineDays)
                        : target.HomeworkDeadlineDays,
                    PlacementAttemptLimit = normalizedSection == "training"
                        ? (request.PlacementAttemptLimit ?? target.PlacementAttemptLimit)
                        : target.PlacementAttemptLimit,
                    RemediationCompletionThresholdPercent = normalizedSection == "training"
                        ? (request.RemediationCompletionThresholdPercent ?? target.RemediationCompletionThresholdPercent)
                        : target.RemediationCompletionThresholdPercent,
                    RemediationThresholdComparison = normalizedSection == "training"
                        ? (request.RemediationThresholdComparison ?? target.RemediationThresholdComparison)
                        : target.RemediationThresholdComparison,
                    RequireOfficialExamEvidence = target.RequireOfficialExamEvidence,
                    RequireAdminRemediationReview = target.RequireAdminRemediationReview,
                    KpiReportingPeriodDays = normalizedSection == "training"
                        ? (request.KpiReportingPeriodDays ?? target.KpiReportingPeriodDays)
                        : target.KpiReportingPeriodDays,
                    TuitionPerSessionN5 = target.TuitionPerSessionN5,
                    TuitionPerSessionN4 = target.TuitionPerSessionN4,
                    TuitionPerSessionN3 = target.TuitionPerSessionN3,
                    TuitionPerSessionN2 = target.TuitionPerSessionN2,
                    RecurringWeekdayPatterns = DeserializePatterns(target.RecurringWeekdayPatternsJson),
                };
            },
            cancellationToken);
    }

    public async Task<ApiResponse<MentorGroupPolicy>> ActivateVersionAsync(
        int policyId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (adminId <= 0)
            return ApiResponse<MentorGroupPolicy>.Fail("Tài khoản quản trị không hợp lệ.", HttpStatusCode.BadRequest);
        if (policyId <= 0)
            return ApiResponse<MentorGroupPolicy>.Fail("Phiên bản chính sách không hợp lệ.", HttpStatusCode.BadRequest);

        try
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;

                var target = await context.MentorGroupPolicies
                    .Include(x => x.LevelLimits)
                        .ThenInclude(l => l.CertificateLevel)
                    .FirstOrDefaultAsync(x => x.Id == policyId && !x.IsArchivedRevision && !x.IsDeleted, cancellationToken);
                if (target is null)
                    return ApiResponse<MentorGroupPolicy>.NotFound("Không tìm thấy phiên bản chính sách được chọn.");
                if (target.IsActive)
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Phiên bản này đang được áp dụng cho toàn hệ thống.",
                        HttpStatusCode.Conflict);

                var previousActiveRows = await context.MentorGroupPolicies
                    .Where(x => x.IsActive && x.Id != policyId && !x.IsArchivedRevision && !x.IsDeleted)
                    .ToListAsync(cancellationToken);
                var previousActive = previousActiveRows.FirstOrDefault();

                if (previousActiveRows.Count > 0)
                {
                    foreach (var row in previousActiveRows)
                        row.IsActive = false;
                    await context.SaveChangesAsync(cancellationToken);
                }

                var nowUtc = DateTime.UtcNow;
                target.IsActive = true;
                target.LastAppliedAtUtc = nowUtc;
                target.LastAppliedByAdminId = adminId;

                context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    ActorUserId = adminId,
                    EntityType = "MentorGroupPolicy",
                    EntityId = target.Version,
                    Action = "ActivatedVersion",
                    BeforeJson = JsonSerializer.Serialize(new
                    {
                        PreviousActiveId = previousActive?.Id,
                        PreviousActiveVersion = previousActive?.Version,
                    }),
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        ActivePolicyId = target.Id,
                        ActivePolicyVersion = target.Version,
                        target.LastAppliedAtUtc,
                        target.LastAppliedByAdminId,
                    }),
                    CreatedAtUtc = nowUtc,
                });

                await context.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);

                var persisted = await LoadDetailedByIdAsync(target.Id, cancellationToken);
                ApplyDeleteEligibility(persisted, isReferenced: false);
                return ApiResponse<MentorGroupPolicy>.Success(
                    persisted,
                    HttpStatusCode.OK,
                    "Đã áp dụng phiên bản chính sách cho toàn hệ thống.");
            });
        }
        catch (DbUpdateException)
        {
            return ApiResponse<MentorGroupPolicy>.Fail(
                "Có thay đổi đồng thời khi áp dụng phiên bản chính sách. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }
    }

    public async Task<ApiResponse<object>> DeleteVersionAsync(
        int policyId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (adminId <= 0)
            return ApiResponse<object>.Fail("Tài khoản quản trị không hợp lệ.", HttpStatusCode.BadRequest);
        if (policyId <= 0)
            return ApiResponse<object>.Fail("Phiên bản chính sách không hợp lệ.", HttpStatusCode.BadRequest);

        try
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;

                var target = await context.MentorGroupPolicies
                    .Include(x => x.LevelLimits)
                        .ThenInclude(l => l.CertificateLevel)
                    .FirstOrDefaultAsync(x => x.Id == policyId && !x.IsArchivedRevision && !x.IsDeleted, cancellationToken);
                if (target is null)
                    return ApiResponse<object>.NotFound("Không tìm thấy phiên bản chính sách được chọn.");
                if (target.IsActive)
                    return ApiResponse<object>.Fail(
                        "Không thể xóa phiên bản đang được áp dụng cho toàn hệ thống.",
                        HttpStatusCode.Conflict);

                var allIdsForVersion = await context.MentorGroupPolicies.AsNoTracking()
                    .Where(p => p.Version == target.Version)
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);

                var referencedIds = await GetReferencedPolicyIdSetAsync(allIdsForVersion, cancellationToken);
                if (referencedIds.Count > 0)
                    return ApiResponse<object>.Fail(
                        "Không thể xóa phiên bản đã được lớp học, lịch dạy, đề xuất hoặc giao dịch tham chiếu.",
                        HttpStatusCode.Conflict);

                var nowUtc = DateTime.UtcNow;
                var beforeLimits = target.GetMaxActiveClassesByLevelCode();
                target.IsDeleted = true;
                target.DeletedAtUtc = nowUtc;
                target.DeletedByAdminId = adminId;

                context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    ActorUserId = adminId,
                    EntityType = "MentorGroupPolicy",
                    EntityId = target.Version,
                    Action = "DeletedVersion",
                    BeforeJson = JsonSerializer.Serialize(BuildAuditSnapshot(target, beforeLimits, null)),
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        target.Id,
                        target.Version,
                        target.IsDeleted,
                        target.DeletedAtUtc,
                        target.DeletedByAdminId,
                    }),
                    CreatedAtUtc = nowUtc,
                });

                await context.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);

                return ApiResponse<object>.Success(new
                {
                    target.Id,
                    target.Version,
                    Deleted = true,
                });
            });
        }
        catch (DbUpdateException)
        {
            return ApiResponse<object>.Fail(
                "Có thay đổi đồng thời khi xóa phiên bản chính sách. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }
    }

    private async Task<ApiResponse<MentorGroupPolicy>> SaveUpdatedPolicyAsync(
        int policyId,
        int adminId,
        int expectedRevision,
        string auditAction,
        Func<MentorGroupPolicy, CreateMentorGroupPolicyRequest> buildMergedRequest,
        CancellationToken cancellationToken)
    {
        if (adminId <= 0)
            return ApiResponse<MentorGroupPolicy>.Fail("Tài khoản quản trị không hợp lệ.", HttpStatusCode.BadRequest);

        var supportedLevelRows = await GetSupportedLevelsAsync(cancellationToken);
        var supportedCodes = supportedLevelRows
            .Select(l => l.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;

                var target = await context.MentorGroupPolicies
                    .Include(x => x.LevelLimits)
                        .ThenInclude(l => l.CertificateLevel)
                    .FirstOrDefaultAsync(x => x.Id == policyId && !x.IsArchivedRevision && !x.IsDeleted, cancellationToken);
                if (target is null)
                    return ApiResponse<MentorGroupPolicy>.NotFound("Không tìm thấy phiên bản chính sách được chọn.");

                if (expectedRevision > 0 && target.Revision != expectedRevision)
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Phiên bản chính sách vừa được quản trị viên khác cập nhật. Vui lòng tải lại trước khi lưu.",
                        HttpStatusCode.Conflict);

                var mergedRequest = buildMergedRequest(target);
                var validationError = Validate(mergedRequest, adminId, supportedCodes, requireExpectedCurrentVersion: false);
                if (validationError is not null)
                    return ApiResponse<MentorGroupPolicy>.Fail(validationError, HttpStatusCode.BadRequest);

                var nowUtc = DateTime.UtcNow;
                var patternsJson = ResolvePatternsJson(mergedRequest.RecurringWeekdayPatterns, target.RecurringWeekdayPatternsJson);
                var beforeLimits = target.GetMaxActiveClassesByLevelCode();
                var beforeSnapshot = BuildAuditSnapshot(target, beforeLimits, target.IsActive ? target.Version : null);

                var referencedIds = await GetReferencedPolicyIdSetAsync([target.Id], cancellationToken);
                var isReferenced = referencedIds.Count > 0;

                MentorGroupPolicy livePolicy;
                Dictionary<string, int> afterLimits;

                if (isReferenced)
                {
                    // Chuẩn bị revision mới và kiểm tra giới hạn cấp độ trước khi lưu trạng thái lưu trữ cho bản ghi cũ.
                    var wasActive = target.IsActive;
                    var originalVersion = target.Version;
                    var nextRevision = target.Revision + 1;
                    var originalEffectiveFrom = target.EffectiveFromUtc;
                    var originalCreatedBy = target.CreatedByAdminId;
                    var originalCreatedAt = target.CreatedAtUtc;
                    var originalLastAppliedAt = target.LastAppliedAtUtc;
                    var originalLastAppliedBy = target.LastAppliedByAdminId;

                    var candidatePolicy = new MentorGroupPolicy
                    {
                        Version = originalVersion,
                        Revision = nextRevision,
                        IsActive = wasActive,
                        IsArchivedRevision = false,
                        IsDeleted = false,
                        EffectiveFromUtc = originalEffectiveFrom,
                        LastAppliedAtUtc = originalLastAppliedAt,
                        LastAppliedByAdminId = originalLastAppliedBy,
                        CreatedByAdminId = originalCreatedBy,
                        CreatedAtUtc = originalCreatedAt,
                        UpdatedAtUtc = nowUtc,
                        UpdatedByAdminId = adminId,
                    };
                    ApplyPolicyFields(candidatePolicy, mergedRequest, patternsJson);
                    (_, afterLimits) = PopulateLevelLimits(candidatePolicy, target, mergedRequest.MaxActiveClassesByLevel, supportedLevelRows);

                    if (afterLimits.Count > 0 && afterLimits.Values.All(v => v <= 0))
                        return ApiResponse<MentorGroupPolicy>.Fail(
                            "Phải cho phép mở lớp ở ít nhất một cấp độ JLPT (giá trị >= 1).",
                            HttpStatusCode.BadRequest);

                    target.IsArchivedRevision = true;
                    target.IsActive = false;
                    await context.SaveChangesAsync(cancellationToken);

                    livePolicy = candidatePolicy;
                    context.MentorGroupPolicies.Add(livePolicy);
                }
                else
                {
                    ApplyPolicyFields(target, mergedRequest, patternsJson);
                    target.Revision += 1;
                    target.UpdatedAtUtc = nowUtc;
                    target.UpdatedByAdminId = adminId;

                    if (target.LevelLimits.Count > 0)
                        context.MentorGroupPolicyLevelLimits.RemoveRange(target.LevelLimits);
                    target.LevelLimits.Clear();

                    (_, afterLimits) = PopulateLevelLimits(target, beforeLimits, mergedRequest.MaxActiveClassesByLevel, supportedLevelRows);
                    if (afterLimits.Count > 0 && afterLimits.Values.All(v => v <= 0))
                        return ApiResponse<MentorGroupPolicy>.Fail(
                            "Phải cho phép mở lớp ở ít nhất một cấp độ JLPT (giá trị >= 1).",
                            HttpStatusCode.BadRequest);

                    livePolicy = target;
                }

                context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    ActorUserId = adminId,
                    EntityType = "MentorGroupPolicy",
                    EntityId = livePolicy.Version,
                    Action = auditAction,
                    BeforeJson = JsonSerializer.Serialize(beforeSnapshot),
                    AfterJson = JsonSerializer.Serialize(BuildAuditSnapshot(livePolicy, afterLimits, livePolicy.IsActive ? livePolicy.Version : null)),
                    CreatedAtUtc = nowUtc,
                });

                await context.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);

                var persisted = await LoadDetailedByIdAsync(livePolicy.Id, cancellationToken);
                var anyVersionReferenced = isReferenced || await IsVersionReferencedAsync(persisted.Version, cancellationToken);
                ApplyDeleteEligibility(persisted, anyVersionReferenced);
                return ApiResponse<MentorGroupPolicy>.Success(persisted);
            });
        }
        catch (DbUpdateException)
        {
            return ApiResponse<MentorGroupPolicy>.Fail(
                "Phiên bản chính sách vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }
    }

    private async Task<bool> IsVersionReferencedAsync(int version, CancellationToken cancellationToken)
    {
        var ids = await context.MentorGroupPolicies.AsNoTracking()
            .Where(p => p.Version == version)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var referenced = await GetReferencedPolicyIdSetAsync(ids, cancellationToken);
        return referenced.Count > 0;
    }

    private async Task<HashSet<int>> GetReferencedPolicyIdSetAsync(
        IReadOnlyCollection<int> policyIds,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<int>();
        if (policyIds.Count == 0)
            return result;

        var cohortRefs = await context.StudyCohorts.AsNoTracking()
            .Where(x => x.GroupPolicyId.HasValue && policyIds.Contains(x.GroupPolicyId.Value))
            .Select(x => x.GroupPolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in cohortRefs) result.Add(id);

        var courseRefs = await context.StudyCourses.AsNoTracking()
            .Where(x => x.GroupPolicyId.HasValue && policyIds.Contains(x.GroupPolicyId.Value))
            .Select(x => x.GroupPolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in courseRefs) result.Add(id);

        var slotRefs = await context.MentorAvailabilities.AsNoTracking()
            .Where(x => x.GroupPolicyId.HasValue && policyIds.Contains(x.GroupPolicyId.Value))
            .Select(x => x.GroupPolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in slotRefs) result.Add(id);

        var proposalRefs = await context.MentorTeachingProposals.AsNoTracking()
            .Where(x => x.GroupPolicyId.HasValue && policyIds.Contains(x.GroupPolicyId.Value))
            .Select(x => x.GroupPolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in proposalRefs) result.Add(id);

        var transferRefs = await context.CohortTransferRequests.AsNoTracking()
            .Where(x => x.PolicyId.HasValue && policyIds.Contains(x.PolicyId.Value))
            .Select(x => x.PolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in transferRefs) result.Add(id);

        var replacementRefs = await context.StudyCohortMentorAssignments.AsNoTracking()
            .Where(x => x.PolicyId.HasValue && policyIds.Contains(x.PolicyId.Value))
            .Select(x => x.PolicyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in replacementRefs) result.Add(id);

        return result;
    }

    private static void ApplyDeleteEligibility(MentorGroupPolicy row, bool isReferenced)
    {
        if (row.IsActive)
        {
            row.CanDelete = false;
            row.DeleteBlockedReason = "Không thể xóa phiên bản đang được áp dụng cho toàn hệ thống.";
        }
        else if (isReferenced)
        {
            row.CanDelete = false;
            row.DeleteBlockedReason = "Phiên bản đang được lớp học, lịch dạy, đề xuất hoặc giao dịch tham chiếu.";
        }
        else
        {
            row.CanDelete = true;
            row.DeleteBlockedReason = null;
        }
    }

    private static void ApplyPolicyFields(
        MentorGroupPolicy policy,
        CreateMentorGroupPolicyRequest request,
        string patternsJson)
    {
        policy.MaxLearners = request.MaxLearners;
        policy.EnrollmentCutoffMinutesBeforeFirstSession = request.EnrollmentCutoffMinutesBeforeFirstSession;
        policy.SessionMinutes = request.SessionMinutes;
        policy.RestMinutes = request.RestMinutes;
        policy.DailySessionCap = request.DailySessionCap;
        policy.WeeklySessionCap = request.WeeklySessionCap;
        policy.PublicationHorizonDays = request.PublicationHorizonDays;
        policy.CancellationLeadHours = request.CancellationLeadHours;
        policy.ReschedulingLeadHours = request.ReschedulingLeadHours;
        policy.HomeworkDeadlineDays = request.HomeworkDeadlineDays;
        policy.PlacementAttemptLimit = request.PlacementAttemptLimit;
        policy.RemediationCompletionThresholdPercent = request.RemediationCompletionThresholdPercent;
        policy.RemediationThresholdComparison = request.RemediationThresholdComparison;
        policy.RequireOfficialExamEvidence = request.RequireOfficialExamEvidence;
        policy.RequireAdminRemediationReview = request.RequireAdminRemediationReview;
        policy.KpiReportingPeriodDays = request.KpiReportingPeriodDays;
        policy.LearnerTransferEnabled = request.LearnerTransferEnabled;
        policy.TransferAllowedAfterSourceStart = request.TransferAllowedAfterSourceStart;
        policy.MentorReplacementEnabled = request.MentorReplacementEnabled;
        policy.MentorReplacementLeadHours = request.MentorReplacementLeadHours;
        policy.RecurringWeekdayPatternsJson = patternsJson;
        policy.CourseDurationWeeks = request.CourseDurationWeeks;
        policy.AllowedSessionsPerWeekCsv = string.Join(',',
            MentorGroupPolicy.ParseSessionsPerWeekCsv(request.AllowedSessionsPerWeekCsv).Distinct().OrderBy(x => x));
        policy.DefaultSessionsPerWeek = request.DefaultSessionsPerWeek;
        policy.CheckoutHoldMinutes = request.CheckoutHoldMinutes;
        policy.MinLearnersToStart = request.MinLearnersToStart;
        policy.MinimumProposalLeadDays = request.MinimumProposalLeadDays;
        policy.MinimumPublishedEnrollmentDays = request.MinimumPublishedEnrollmentDays;
        policy.TuitionPerSessionN5 = decimal.Truncate(request.TuitionPerSessionN5);
        policy.TuitionPerSessionN4 = decimal.Truncate(request.TuitionPerSessionN4);
        policy.TuitionPerSessionN3 = decimal.Truncate(request.TuitionPerSessionN3);
        policy.TuitionPerSessionN2 = decimal.Truncate(request.TuitionPerSessionN2);
        policy.MaxTotalActiveClassesPerMentor = request.MaxTotalActiveClassesPerMentor;
    }

    private static (Dictionary<string, int> Before, Dictionary<string, int> After) PopulateLevelLimits(
        MentorGroupPolicy targetPolicy,
        MentorGroupPolicy baselinePolicy,
        Dictionary<string, int>? requestedLimits,
        IReadOnlyList<SupportedPolicyLevelDto> supportedLevels)
    {
        var beforeLimits = baselinePolicy.GetMaxActiveClassesByLevelCode();
        var (_, afterLimits) = PopulateLevelLimits(targetPolicy, beforeLimits, requestedLimits, supportedLevels);
        return (beforeLimits, afterLimits);
    }

    private static (Dictionary<string, int> Before, Dictionary<string, int> After) PopulateLevelLimits(
        MentorGroupPolicy targetPolicy,
        Dictionary<string, int> beforeLimits,
        Dictionary<string, int>? requestedLimits,
        IReadOnlyList<SupportedPolicyLevelDto> supportedLevels)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (requestedLimits is not null)
        {
            foreach (var kv in requestedLimits)
            {
                var key = kv.Key?.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(key))
                    normalized[key] = kv.Value;
            }
        }

        var afterLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var lvl in supportedLevels)
        {
            var limit = normalized.TryGetValue(lvl.Code, out var configured)
                ? configured
                : (beforeLimits.TryGetValue(lvl.Code, out var prev) ? prev : 1);
            afterLimits[lvl.Code] = limit;
            targetPolicy.LevelLimits.Add(new MentorGroupPolicyLevelLimit
            {
                CertificateLevelId = lvl.Id,
                MaxActiveClasses = limit,
            });
        }

        return (beforeLimits, afterLimits);
    }

    private static string ResolvePatternsJson(List<List<int>>? patterns, string? fallbackJson)
    {
        if (patterns is { Count: > 0 })
            return JsonSerializer.Serialize(patterns);
        return !string.IsNullOrWhiteSpace(fallbackJson) ? fallbackJson : "[[1,3,5],[2,4,6]]";
    }

    private static List<List<int>> DeserializePatterns(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<List<int>>>(json ?? "[]") ?? [[1, 3, 5], [2, 4, 6]];
        }
        catch
        {
            return [[1, 3, 5], [2, 4, 6]];
        }
    }

    private Task<MentorGroupPolicy> LoadDetailedByIdAsync(int policyId, CancellationToken cancellationToken)
        => context.MentorGroupPolicies.AsNoTracking()
            .Include(x => x.LevelLimits)
                .ThenInclude(l => l.CertificateLevel)
            .Include(x => x.CreatedByAdmin)
            .Include(x => x.LastAppliedByAdmin)
            .Include(x => x.UpdatedByAdmin)
            .FirstAsync(x => x.Id == policyId, cancellationToken);

    private static object BuildAuditSnapshot(
        MentorGroupPolicy policy,
        Dictionary<string, int> levelLimits,
        int? activeVersion) => new
        {
            policy.Version,
            policy.Revision,
            policy.IsActive,
            ActiveVersion = activeVersion,
            policy.LastAppliedAtUtc,
            policy.LastAppliedByAdminId,
            policy.UpdatedAtUtc,
            policy.UpdatedByAdminId,
            policy.MaxLearners,
            policy.MinLearnersToStart,
            policy.MaxTotalActiveClassesPerMentor,
            MaxActiveClassesByLevel = levelLimits,
            policy.EnrollmentCutoffMinutesBeforeFirstSession,
            policy.SessionMinutes,
            policy.RestMinutes,
            policy.DailySessionCap,
            policy.WeeklySessionCap,
            policy.PublicationHorizonDays,
            policy.CancellationLeadHours,
            policy.ReschedulingLeadHours,
            policy.HomeworkDeadlineDays,
            policy.PlacementAttemptLimit,
            policy.RemediationCompletionThresholdPercent,
            policy.RemediationThresholdComparison,
            policy.KpiReportingPeriodDays,
            policy.LearnerTransferEnabled,
            policy.MentorReplacementEnabled,
            policy.MentorReplacementLeadHours,
            policy.CourseDurationWeeks,
            policy.DefaultSessionsPerWeek,
            policy.AllowedSessionsPerWeekCsv,
            policy.CheckoutHoldMinutes,
            policy.MinimumProposalLeadDays,
            policy.MinimumPublishedEnrollmentDays,
            policy.TuitionPerSessionN5,
            policy.TuitionPerSessionN4,
            policy.TuitionPerSessionN3,
            policy.TuitionPerSessionN2,
        };

    private static string? Validate(
        CreateMentorGroupPolicyRequest request,
        int adminId,
        IReadOnlySet<string> supportedCodes,
        bool requireExpectedCurrentVersion = true)
    {
        if (adminId <= 0) return "Tài khoản quản trị không hợp lệ.";
        if (requireExpectedCurrentVersion && request.ExpectedCurrentVersion < 1) return "Phiên bản hiện tại không hợp lệ.";
        if (request.MaxLearners is < 1 or > 50)
            return "Sĩ số tối đa mỗi lớp phải từ 1 đến 50 học viên.";
        if (request.EnrollmentCutoffMinutesBeforeFirstSession is < 0 or > 10080)
            return "Thời điểm đóng tuyển sinh phải từ lúc buổi đầu bắt đầu hoặc sớm hơn tối đa 7 ngày (10.080 phút).";
        if (request.SessionMinutes is < 30 or > 240 || request.RestMinutes is < 0 or > 120
            || request.DailySessionCap is < 1 or > 12 || request.PublicationHorizonDays is < 1 or > 365)
            return "Thời lượng buổi học (30–240 phút), thời gian nghỉ (0–120 phút), số ca/ngày (1–12) hoặc phạm vi công bố (1–365 ngày) không hợp lệ.";
        if (request.WeeklySessionCap is < 0 or > 84)
            return "Giới hạn số buổi dạy mỗi tuần phải từ 0 (không giới hạn) đến 84.";
        if (request.CancellationLeadHours is < 0 or > 720 || request.ReschedulingLeadHours is < 0 or > 720
            || request.HomeworkDeadlineDays is < 1 or > 365 || request.PlacementAttemptLimit is < 1 or > 20
            || request.KpiReportingPeriodDays is < 1 or > 366)
            return "Chính sách hủy, đổi lịch, bài tập, đánh giá hoặc kỳ báo cáo không hợp lệ.";
        if (request.MentorReplacementLeadHours is < 0 or > 720)
            return "Thời gian báo trước khi thay Giáo viên phải từ 0 đến 720 giờ.";
        if (request.TransferAllowedAfterSourceStart && !request.LearnerTransferEnabled)
            return "Không thể cho phép chuyển lớp sau ngày bắt đầu khi chức năng chuyển lớp đang tắt.";
        if (request.RemediationCompletionThresholdPercent is <= 0 or > 100)
            return "Ngưỡng hoàn thành bài tập phải lớn hơn 0% và không vượt quá 100%.";
        if (request.RemediationThresholdComparison is not (">" or ">="))
            return "Cách so sánh ngưỡng hoàn thành bài tập không hợp lệ.";
        if (!request.RequireOfficialExamEvidence || !request.RequireAdminRemediationReview)
            return "Yêu cầu học lại luôn cần minh chứng JLPT chính thức và quản trị viên xét duyệt.";
        if (request.RecurringWeekdayPatterns is { Count: > 0 })
        {
            if (request.RecurringWeekdayPatterns.Any(pattern => pattern.Count == 0
                    || pattern.Any(day => day is < 0 or > 6)
                    || pattern.Distinct().Count() != pattern.Count))
                return "Mẫu lịch lặp phải gồm các thứ hợp lệ, không trùng nhau.";
            var keys = request.RecurringWeekdayPatterns
                .Select(pattern => string.Join(',', pattern.OrderBy(day => day))).ToList();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
                return "Không được tạo hai mẫu lịch lặp giống nhau.";
        }
        if (request.CourseDurationWeeks is < 1 or > 52)
            return "Thời lượng khóa học phải từ 1 đến 52 tuần.";
        var rawParts = (request.AllowedSessionsPerWeekCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allowed = MentorGroupPolicy.ParseSessionsPerWeekCsv(request.AllowedSessionsPerWeekCsv);
        if (rawParts.Length == 0 || allowed.Count != rawParts.Length
            || allowed.Any(x => x < 1) || allowed.Distinct().Count() != allowed.Count)
            return "Danh sách số buổi mỗi tuần phải gồm các số nguyên dương khác nhau.";

        const int operatingMinutesPerDay = 15 * 60;
        var maxPhysicalSlotsPerDay = Math.Max(1, (operatingMinutesPerDay + request.RestMinutes) / (request.SessionMinutes + request.RestMinutes));
        var effectiveMaxPerDay = Math.Min(request.DailySessionCap, maxPhysicalSlotsPerDay);
        var maxPerWeekFromDaily = effectiveMaxPerDay * 7;
        var maxAllowedPerWeek = allowed.Max();
        if (request.WeeklySessionCap > 0 && maxAllowedPerWeek > request.WeeklySessionCap)
            return $"Số buổi tối đa mỗi tuần của một lớp ({maxAllowedPerWeek}) không được vượt quá giới hạn dạy tối đa mỗi tuần của giáo viên ({request.WeeklySessionCap} buổi).";
        if (maxAllowedPerWeek > maxPerWeekFromDaily)
            return $"Số buổi tối đa mỗi tuần ({maxAllowedPerWeek}) vượt quá khả năng tổ chức lịch thực tế ({effectiveMaxPerDay} buổi mỗi ngày × 7 ngày = {maxPerWeekFromDaily} buổi).";
        if (!allowed.Contains(request.DefaultSessionsPerWeek))
            return "Số buổi mặc định mỗi tuần phải nằm trong khoảng số buổi được phép.";
        if (request.CheckoutHoldMinutes is < 1 or > 60)
            return "Thời gian giữ chỗ khi thanh toán phải từ 1 đến 60 phút.";
        if (request.MinLearnersToStart < 1 || request.MinLearnersToStart > request.MaxLearners)
            return "Số học viên tối thiểu để mở lớp phải từ 1 đến sĩ số tối đa.";
        if (request.MaxTotalActiveClassesPerMentor is < 1 or > 50)
            return "Tổng số lớp đồng thời tối đa mỗi Giáo viên phải từ 1 đến 50.";
        if (request.MaxActiveClassesByLevel is { Count: > 0 })
        {
            foreach (var entry in request.MaxActiveClassesByLevel)
            {
                var code = entry.Key?.Trim().ToUpperInvariant() ?? string.Empty;
                if (!supportedCodes.Contains(code))
                    return $"Cấp độ \"{entry.Key}\" không nằm trong danh sách cấp độ lớp nhóm được hỗ trợ.";
                if (entry.Value is < 0 or > 50)
                    return $"Giới hạn số lớp đồng thời cho cấp độ {code} phải từ 0 đến 50.";
            }
            if (supportedCodes.Count > 0
                && request.MaxActiveClassesByLevel.Count >= supportedCodes.Count
                && request.MaxActiveClassesByLevel.Values.All(v => v <= 0))
                return "Phải cho phép mở lớp ở ít nhất một cấp độ JLPT (giá trị >= 1).";
        }
        if (request.MinimumProposalLeadDays is < 1 or > 180)
            return "Số ngày gửi đề xuất trước ngày bắt đầu phải từ 1 đến 180 ngày.";
        if (request.MinimumPublishedEnrollmentDays is < 1 or > 180)
            return "Thời gian tuyển sinh tối thiểu phải từ 1 đến 180 ngày.";
        if (request.MinimumPublishedEnrollmentDays > request.MinimumProposalLeadDays)
            return "Thời gian tuyển sinh tối thiểu không được lớn hơn số ngày gửi đề xuất trước ngày bắt đầu.";
        const decimal MaxPerSessionVnd = 50_000_000m;
        var tuitionEntries = new (string Level, decimal Amount)[]
        {
            ("N5", request.TuitionPerSessionN5),
            ("N4", request.TuitionPerSessionN4),
            ("N3", request.TuitionPerSessionN3),
            ("N2", request.TuitionPerSessionN2),
        };
        foreach (var (level, amount) in tuitionEntries)
        {
            if (amount <= 0m || amount > MaxPerSessionVnd || decimal.Truncate(amount) != amount)
                return $"Đơn giá học phí mỗi buổi cho cấp độ {level} phải là số nguyên dương hợp lệ (VND).";
        }
        return null;
    }
}
