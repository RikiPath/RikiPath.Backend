using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorCompensation;

namespace RikiPath.Application.Services;

public sealed class MentorPlanService(IUnitOfWork uow, IClaimService claims) : IMentorPlanService
{
    public async Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetAsync(CancellationToken ct)
    {
        if (claims.GetUserClaim().Role != Role.Admin)
            return ApiResponse<List<MentorMeetingPlanResponse>>.Fail("Chỉ admin được quản lý gói.", System.Net.HttpStatusCode.Forbidden);

        var plans = await uow.SubscriptionPlans.Query()
            .Include(p => p.CertificateLevel)
            .Where(p => !p.IsDeleted && p.MeetingSessionCount > 0)
            .OrderBy(p => p.CertificateLevelId)
            .ThenBy(p => p.MeetingSessionCount)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        return ApiResponse<List<MentorMeetingPlanResponse>>.Success(plans.Select(p => new MentorMeetingPlanResponse
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            DurationDays = p.MeetingSessionCount switch { 1 => 30, 3 => 90, _ => p.DurationDays },
            MeetingSessionCount = p.MeetingSessionCount,
            IsActive = p.IsActive,
            CertificateLevelId = p.CertificateLevelId,
            JlptLevel = p.CertificateLevel?.Code ?? ResolveLevelCode(p.CertificateLevelId)
        }).ToList());
    }

    public async Task<ApiResponse<int>> SaveAsync(int? id, SaveMentorPlanRequest r, CancellationToken ct)
    {
        if (claims.GetUserClaim().Role != Role.Admin)
            return ApiResponse<int>.Fail("Chỉ admin được quản lý gói.", System.Net.HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200 || r.Description?.Length > 2000
            || r.MeetingSessionCount is not (1 or 3) || r.Price <= 0 || r.Price != decimal.Truncate(r.Price) || r.Price > int.MaxValue)
            return ApiResponse<int>.Fail("Nhập tên gói, 1 hoặc 3 buổi và giá VND nguyên dương.");

        int? resolvedLevelId = null;
        if (!string.IsNullOrWhiteSpace(r.JlptLevel))
        {
            if (!MentorCompensationPolicy.TryNormalizeSupportedLevel(r.JlptLevel, out var normalizedLevel))
                return ApiResponse<int>.Fail("Gói tư vấn chỉ hỗ trợ các trình độ JLPT N5, N4, N3 hoặc N2 (không hỗ trợ N1).");

            var certLevel = await uow.CertificateLevels.Query()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code.ToUpper() == normalizedLevel, ct);
            resolvedLevelId = certLevel?.Id ?? MentorCompensationPolicy.ResolveDefaultCertificateLevelId(normalizedLevel);
        }

        var plan = id.HasValue ? await uow.SubscriptionPlans.GetByIdAsync(id.Value, ct) : new SubscriptionPlan();
        if (plan is null || plan.IsDeleted)
            return ApiResponse<int>.NotFound();

        if (id.HasValue && plan.MeetingSessionCount is not (1 or 3))
            return ApiResponse<int>.Fail("Không chuyển đổi gói tự học hoặc gói lịch sử sang gói tư vấn mới.");

        plan.Name = r.Name.Trim();
        plan.Description = r.Description?.Trim();
        plan.MeetingSessionCount = r.MeetingSessionCount;
        plan.DurationDays = r.MeetingSessionCount == 1 ? 30 : 90;
        plan.Price = r.Price;
        plan.IsActive = r.IsActive;
        if (resolvedLevelId.HasValue || !string.IsNullOrWhiteSpace(r.JlptLevel))
        {
            plan.CertificateLevelId = resolvedLevelId;
        }

        if (!id.HasValue)
            await uow.SubscriptionPlans.AddAsync(plan, ct);

        await uow.SaveChangesAsync(ct);
        return ApiResponse<int>.Success(plan.Id);
    }

    public static string? ResolveLevelCode(int? certificateLevelId)
        => MentorCompensationPolicy.ResolveDefaultLevelCode(certificateLevelId);
}
