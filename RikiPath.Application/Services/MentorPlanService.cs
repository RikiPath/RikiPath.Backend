using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
namespace RikiPath.Application.Services;
public sealed class MentorPlanService(IUnitOfWork uow, IClaimService claims) : IMentorPlanService
{
    public async Task<ApiResponse<List<MentorMeetingPlanResponse>>> GetAsync(CancellationToken ct)
    {
        if (claims.GetUserClaim().Role != Role.Admin) return ApiResponse<List<MentorMeetingPlanResponse>>.Fail("Chỉ admin được quản lý gói.", System.Net.HttpStatusCode.Forbidden);
        var plans = await uow.SubscriptionPlans.Query().Where(p => !p.IsDeleted && p.MeetingSessionCount > 0).OrderBy(p => p.Id).ToListAsync(ct);
        return ApiResponse<List<MentorMeetingPlanResponse>>.Success(plans.Select(p => new MentorMeetingPlanResponse {
            Id = p.Id, Name = p.Name, Description = p.Description, Price = p.Price,
            DurationDays = p.MeetingSessionCount switch { 1 => 14, 3 => 35, _ => p.DurationDays },
            MeetingSessionCount = p.MeetingSessionCount, IsActive = p.IsActive
        }).ToList());
    }
    public async Task<ApiResponse<int>> SaveAsync(int? id, SaveMentorPlanRequest r, CancellationToken ct)
    {
        if (claims.GetUserClaim().Role != Role.Admin) return ApiResponse<int>.Fail("Chỉ admin được quản lý gói.", System.Net.HttpStatusCode.Forbidden);
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200 || r.Description?.Length > 2000
            || r.MeetingSessionCount is not (1 or 3) || r.Price <= 0 || r.Price != decimal.Truncate(r.Price) || r.Price > int.MaxValue)
            return ApiResponse<int>.Fail("Nhập tên gói, 1 hoặc 3 buổi và giá VND nguyên dương.");
        var plan = id.HasValue ? await uow.SubscriptionPlans.GetByIdAsync(id.Value, ct) : new SubscriptionPlan();
        if (plan is null || plan.IsDeleted) return ApiResponse<int>.NotFound();
        if (id.HasValue && plan.MeetingSessionCount is not (1 or 3)) return ApiResponse<int>.Fail("Không chuyển đổi gói tự học hoặc gói lịch sử sang gói tư vấn mới.");
        plan.Name = r.Name.Trim(); plan.Description = r.Description?.Trim(); plan.MeetingSessionCount = r.MeetingSessionCount;
        plan.DurationDays = r.MeetingSessionCount == 1 ? 14 : 35; plan.Price = r.Price; plan.IsActive = r.IsActive;
        if (!id.HasValue) await uow.SubscriptionPlans.AddAsync(plan, ct);
        await uow.SaveChangesAsync(ct);
        return ApiResponse<int>.Success(plan.Id);
    }
}
