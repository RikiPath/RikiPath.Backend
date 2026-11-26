using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class CohortEnrollmentEntitlementService(IUnitOfWork uow, IClaimService claims)
    : ICohortEnrollmentEntitlementService
{
    private static readonly HashSet<string> AllowedSourceTypes = new(StringComparer.Ordinal)
    {
        "ScholarshipGrant",
        "RemediationApproved",
        "PartnerSponsor"
    };

    private static readonly HashSet<string> ForbiddenCreditSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConsultationCredit",
        "Credit1on1",
        "OneOnOneCredit",
        "MentorCreditGrant"
    };

    public async Task<ApiResponse<object>> GrantAsync(
        int cohortId,
        GrantCohortEntitlementRequest request,
        CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Admin)
            return ApiResponse<object>.Fail("Chỉ quản trị viên mới được cấp quyền ghi danh lớp nhóm.", HttpStatusCode.Forbidden);

        var sourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "AdminManualGrant" : request.SourceType.Trim();
        if (string.Equals(sourceType, "AdminManualGrant", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<object>.Fail(
                "Cấp quyền ghi danh thủ công đã ngừng sử dụng. Học viên đăng ký và thanh toán trực tiếp theo luồng giữ chỗ — thanh toán tự động.",
                HttpStatusCode.Conflict);
        }
        if (ForbiddenCreditSources.Contains(sourceType))
        {
            return ApiResponse<object>.Fail(
                "Vé tư vấn 1-1 không thể chuyển đổi hoặc cấp dưới dạng quyền ghi danh lớp nhóm.",
                HttpStatusCode.BadRequest);
        }
        if (!AllowedSourceTypes.Contains(sourceType))
        {
            return ApiResponse<object>.Fail(
                "Loại nguồn quyền ghi danh không hợp lệ. Chỉ chấp nhận: ScholarshipGrant, RemediationApproved, PartnerSponsor.",
                HttpStatusCode.BadRequest);
        }
        if (request.Note?.Trim().Length > 500)
            return ApiResponse<object>.Fail("Ghi chú tối đa 500 ký tự.", HttpStatusCode.BadRequest);
        if (request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value <= DateTime.UtcNow)
            return ApiResponse<object>.Fail("Thời hạn quyền ghi danh phải ở tương lai.", HttpStatusCode.BadRequest);

        var cohort = await uow.StudyCohorts.Query()
            .AsNoTracking()
            .Include(x => x.Plan)
            .SingleOrDefaultAsync(x => x.Id == cohortId, ct);
        if (cohort is null)
            return ApiResponse<object>.Fail("Không tìm thấy lớp học định kỳ.", HttpStatusCode.NotFound);

        var learner = await uow.UserAccounts.Query()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.LearnerId && !x.IsDeleted && x.Role == Role.Learner, ct);
        if (learner is null)
            return ApiResponse<object>.Fail("Không tìm thấy học viên hợp lệ.", HttpStatusCode.NotFound);

        var reference = string.IsNullOrWhiteSpace(request.EntitlementReference)
            ? $"ENT-C{cohortId}-U{request.LearnerId}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
            : request.EntitlementReference.Trim();
        if (reference.Length > 100)
            return ApiResponse<object>.Fail("Mã tham chiếu quyền ghi danh tối đa 100 ký tự.", HttpStatusCode.BadRequest);

        var now = DateTime.UtcNow;
        var hasActive = await uow.CohortEnrollmentEntitlements.Query()
            .AnyAsync(x => !x.IsDeleted
                && x.StudyCohortId == cohortId
                && x.LearnerId == request.LearnerId
                && x.Status == "Active"
                && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now), ct);
        if (hasActive)
        {
            return ApiResponse<object>.Fail(
                "Học viên đã có quyền ghi danh đang hoạt động cho lớp nhóm này.",
                HttpStatusCode.Conflict);
        }

        if (await uow.CohortEnrollmentEntitlements.Query()
            .AnyAsync(x => x.EntitlementReference == reference, ct))
        {
            return ApiResponse<object>.Fail("Mã tham chiếu quyền ghi danh đã tồn tại.", HttpStatusCode.Conflict);
        }

        var row = new CohortEnrollmentEntitlement
        {
            StudyCohortId = cohortId,
            PlanId = cohort.PlanId,
            LearnerId = request.LearnerId,
            EntitlementReference = reference,
            SourceType = sourceType,
            Status = "Active",
            Note = request.Note?.Trim(),
            GrantedById = actor.Id,
            GrantedAtUtc = now,
            ExpiresAtUtc = request.ExpiresAtUtc?.ToUniversalTime()
        };

        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.CohortEnrollmentEntitlements.AddAsync(row, token);
            await uow.SaveChangesAsync(token);
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = actor.Id,
                EntityType = "CohortEnrollmentEntitlement",
                EntityId = row.Id,
                Action = "Granted",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { row.Id, row.StudyCohortId, row.LearnerId, row.EntitlementReference, row.SourceType }),
                Reason = row.Note,
                CreatedAtUtc = now,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);

        return ApiResponse<object>.Created(new
        {
            row.Id,
            row.StudyCohortId,
            CohortName = cohort.Name,
            row.PlanId,
            row.LearnerId,
            LearnerName = $"{learner.FirstName} {learner.LastName}".Trim(),
            LearnerEmail = learner.Email,
            row.EntitlementReference,
            row.SourceType,
            row.Status,
            row.Note,
            row.GrantedById,
            row.GrantedAtUtc,
            row.ExpiresAtUtc
        });
    }

    public async Task<ApiResponse<object>> ListForCohortAsync(int cohortId, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Admin)
            return ApiResponse<object>.Fail("Chỉ quản trị viên mới được xem danh sách quyền ghi danh lớp nhóm.", HttpStatusCode.Forbidden);

        var rows = await uow.CohortEnrollmentEntitlements.Query()
            .AsNoTracking()
            .Include(x => x.Learner)
            .Include(x => x.StudyCohort)
            .Where(x => !x.IsDeleted && x.StudyCohortId == cohortId)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.StudyCohortId,
                CohortName = x.StudyCohort.Name,
                x.PlanId,
                x.LearnerId,
                LearnerName = $"{x.Learner.FirstName} {x.Learner.LastName}".Trim(),
                LearnerEmail = x.Learner.Email,
                x.EntitlementReference,
                x.SourceType,
                x.Status,
                x.Note,
                x.GrantedById,
                x.GrantedAtUtc,
                x.ExpiresAtUtc,
                x.ConsumedAtUtc,
                x.ConsumedByCourseId,
                x.RevokedAtUtc,
                x.RevokedById,
                x.RevocationReason
            })
            .ToListAsync(ct);

        return ApiResponse<object>.Success(rows);
    }

    public async Task<ApiResponse<object>> RevokeAsync(
        int entitlementId,
        RevokeCohortEntitlementRequest request,
        CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Admin)
            return ApiResponse<object>.Fail("Chỉ quản trị viên mới được thu hồi quyền ghi danh.", HttpStatusCode.Forbidden);

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return ApiResponse<object>.Fail("Cần nhập lý do thu hồi (tối đa 500 ký tự).", HttpStatusCode.BadRequest);

        var row = await uow.CohortEnrollmentEntitlements.Query()
            .SingleOrDefaultAsync(x => x.Id == entitlementId && !x.IsDeleted, ct);
        if (row is null)
            return ApiResponse<object>.Fail("Không tìm thấy quyền ghi danh.", HttpStatusCode.NotFound);
        if (row.Status != "Active")
            return ApiResponse<object>.Fail("Chỉ có thể thu hồi quyền ghi danh đang ở trạng thái Active.", HttpStatusCode.Conflict);

        var revokeNow = DateTime.UtcNow;
        row.Status = "Revoked";
        row.RevokedAtUtc = revokeNow;
        row.RevokedById = actor.Id;
        row.RevocationReason = reason;
        row.ModifiedDate = revokeNow;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            uow.CohortEnrollmentEntitlements.Update(row);
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = actor.Id,
                EntityType = "CohortEnrollmentEntitlement",
                EntityId = row.Id,
                Action = "Revoked",
                BeforeJson = System.Text.Json.JsonSerializer.Serialize(new { row.Id, Status = "Active" }),
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { row.Id, Status = "Revoked" }),
                Reason = reason,
                CreatedAtUtc = revokeNow,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);

        return ApiResponse<object>.Success(new
        {
            row.Id,
            row.StudyCohortId,
            row.LearnerId,
            row.EntitlementReference,
            row.Status,
            row.RevokedAtUtc,
            row.RevokedById,
            row.RevocationReason
        });
    }

    public async Task<ApiResponse<object>> MyEntitlementsAsync(CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Learner)
            return ApiResponse<object>.Fail("Chỉ học viên mới xem được danh sách quyền ghi danh cá nhân.", HttpStatusCode.Forbidden);

        var now = DateTime.UtcNow;
        var rows = await uow.CohortEnrollmentEntitlements.Query()
            .AsNoTracking()
            .Include(x => x.StudyCohort).ThenInclude(c => c.Plan).ThenInclude(p => p.CertificateLevel)
            .Where(x => !x.IsDeleted && x.LearnerId == actor.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.StudyCohortId,
                CohortName = x.StudyCohort.Name,
                Level = x.StudyCohort.Plan.CertificateLevel.Code,
                StartsAt = x.StudyCohort.StartsAt,
                x.PlanId,
                x.EntitlementReference,
                x.SourceType,
                x.Status,
                IsUsable = x.Status == "Active" && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now),
                x.Note,
                x.GrantedAtUtc,
                x.ExpiresAtUtc,
                x.ConsumedAtUtc,
                x.ConsumedByCourseId,
                x.RevokedAtUtc,
                x.RevocationReason
            })
            .ToListAsync(ct);

        return ApiResponse<object>.Success(rows);
    }
}
