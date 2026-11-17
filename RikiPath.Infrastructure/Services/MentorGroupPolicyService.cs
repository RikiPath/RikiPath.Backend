using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorGroupPolicyService(AppDbContext context, AppSettings? settings = null) : IMentorGroupPolicyService
{
    public Task<MentorGroupPolicy?> GetEffectiveAsync(
        DateTime sessionStartUtc, CancellationToken cancellationToken = default)
        => context.MentorGroupPolicies.AsNoTracking()
            .Where(x => x.EffectiveFromUtc <= sessionStartUtc)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MentorGroupPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
        => await context.MentorGroupPolicies.AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

    public async Task<ApiResponse<MentorGroupPolicy>> CreateVersionAsync(
        CreateMentorGroupPolicyRequest request,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var error = Validate(request, adminId);
        // Quyết định chính thức của Product Owner: không chuyển lớp sau khi khóa học bắt đầu. Chỉ chặn phiên bản MỚI;
        // các phiên bản đã lưu giữ nguyên (kể cả khi có cấu hình phê duyệt cũ).
        if (error is null && request.TransferAllowedAfterSourceStart)
            error = "Không thể bật chuyển lớp sau khai giảng: học viên không được chuyển lớp sau khi khóa học đã bắt đầu.";
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
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latest is null || latest.Version != request.ExpectedCurrentVersion)
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Phiên bản chính sách đã thay đổi. Vui lòng tải lại trước khi lưu.",
                        HttpStatusCode.Conflict);
                if (request.EffectiveFromUtc <= latest.EffectiveFromUtc)
                    return ApiResponse<MentorGroupPolicy>.Fail(
                        "Ngày áp dụng phải sau ngày áp dụng của phiên bản hiện tại.",
                        HttpStatusCode.Conflict);

                var policy = new MentorGroupPolicy
                {
                    Version = latest.Version + 1,
                    EffectiveFromUtc = request.EffectiveFromUtc,
                    MaxLearners = request.MaxLearners,
                    EnrollmentCutoffMinutesBeforeFirstSession = request.EnrollmentCutoffMinutesBeforeFirstSession,
                    SessionMinutes = request.SessionMinutes,
                    RestMinutes = request.RestMinutes,
                    DailySessionCap = request.DailySessionCap,
                    WeeklySessionCap = request.WeeklySessionCap,
                    PublicationHorizonDays = request.PublicationHorizonDays,
                    CancellationLeadHours = request.CancellationLeadHours,
                    ReschedulingLeadHours = request.ReschedulingLeadHours,
                    HomeworkDeadlineDays = request.HomeworkDeadlineDays,
                    PlacementAttemptLimit = request.PlacementAttemptLimit,
                    RemediationCompletionThresholdPercent = request.RemediationCompletionThresholdPercent,
                    RemediationThresholdComparison = request.RemediationThresholdComparison,
                    RequireOfficialExamEvidence = request.RequireOfficialExamEvidence,
                    RequireAdminRemediationReview = request.RequireAdminRemediationReview,
                    KpiReportingPeriodDays = request.KpiReportingPeriodDays,
                    LearnerTransferEnabled = request.LearnerTransferEnabled,
                    TransferAllowedAfterSourceStart = request.TransferAllowedAfterSourceStart,
                    MentorReplacementEnabled = request.MentorReplacementEnabled,
                    MentorReplacementLeadHours = request.MentorReplacementLeadHours,
                    RecurringWeekdayPatternsJson = JsonSerializer.Serialize(request.RecurringWeekdayPatterns),
                    CourseDurationWeeks = request.CourseDurationWeeks,
                    AllowedSessionsPerWeekCsv = string.Join(',',
                        MentorGroupPolicy.ParseSessionsPerWeekCsv(request.AllowedSessionsPerWeekCsv).OrderBy(x => x)),
                    DefaultSessionsPerWeek = request.DefaultSessionsPerWeek,
                    CheckoutHoldMinutes = request.CheckoutHoldMinutes,
                    MinLearnersToStart = request.MinLearnersToStart,
                    CreatedByAdminId = adminId,
                    CreatedAtUtc = DateTime.UtcNow
                };
                context.MentorGroupPolicies.Add(policy);
                await context.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return ApiResponse<MentorGroupPolicy>.Created(policy);
            });
        }
        catch (DbUpdateException)
        {
            return ApiResponse<MentorGroupPolicy>.Fail(
                "Phiên bản chính sách đã được tạo bởi yêu cầu khác. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }
    }

    private static string? Validate(CreateMentorGroupPolicyRequest request, int adminId)
    {
        if (adminId <= 0) return "Tài khoản quản trị không hợp lệ.";
        if (request.ExpectedCurrentVersion < 1) return "Phiên bản hiện tại không hợp lệ.";
        if (request.EffectiveFromUtc.Kind != DateTimeKind.Utc || request.EffectiveFromUtc <= DateTime.UtcNow)
            return "Ngày áp dụng phải là thời điểm UTC trong tương lai.";
        var localEffective = TimeZoneInfo.ConvertTimeFromUtc(request.EffectiveFromUtc,
            TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));
        if (TimeOnly.FromDateTime(localEffective) != TimeOnly.MinValue)
            return "Chính sách phải bắt đầu từ 00:00 theo giờ Việt Nam để tránh đổi quy tắc giữa buổi học.";
        if (request.MaxLearners is < 1 or > 4)
            return "Sức chứa phải từ 1 đến 4 học viên theo giới hạn đã được phê duyệt.";
        if (request.EnrollmentCutoffMinutesBeforeFirstSession is < 0 or > 10080)
            return "Thời điểm khóa đăng ký phải từ lúc buổi đầu bắt đầu hoặc sớm hơn tối đa 7 ngày.";
        if (request.SessionMinutes is < 30 or > 180 || request.RestMinutes is < 0 or > 120
            || request.DailySessionCap is < 1 or > 12 || request.PublicationHorizonDays is < 1 or > 365)
            return "Thời lượng buổi học, thời gian nghỉ hoặc phạm vi công bố không hợp lệ.";
        if (request.WeeklySessionCap is < 0 or > 84)
            return "Giới hạn số buổi dạy mỗi tuần phải từ 0 (không giới hạn) đến 84.";
        if (request.CancellationLeadHours is < 0 or > 720 || request.ReschedulingLeadHours is < 0 or > 720
            || request.HomeworkDeadlineDays is < 1 or > 365 || request.PlacementAttemptLimit is < 1 or > 20
            || request.KpiReportingPeriodDays is < 1 or > 366)
            return "Chính sách hủy, đổi lịch, bài tập, đánh giá hoặc kỳ báo cáo không hợp lệ.";
        if (request.MentorReplacementLeadHours is < 0 or > 720)
            return "Thời gian báo trước khi thay cố vấn phải từ 0 đến 720 giờ.";
        if (request.TransferAllowedAfterSourceStart && !request.LearnerTransferEnabled)
            return "Không thể cho phép chuyển lớp sau khai giảng khi chức năng chuyển lớp đang tắt.";
        if (request.RemediationCompletionThresholdPercent is <= 0 or > 100)
            return "Ngưỡng hoàn thành bài tập phải lớn hơn 0% và không vượt quá 100%.";
        if (request.RemediationThresholdComparison is not (">" or ">="))
            return "Cách so sánh ngưỡng hoàn thành bài tập không hợp lệ.";
        if (!request.RequireOfficialExamEvidence || !request.RequireAdminRemediationReview)
            return "Yêu cầu học lại luôn cần minh chứng JLPT chính thức và quản trị viên xét duyệt.";
        if (request.RecurringWeekdayPatterns.Count == 0
            || request.RecurringWeekdayPatterns.Any(pattern => pattern.Count == 0
                || pattern.Any(day => day is < 0 or > 6)
                || pattern.Distinct().Count() != pattern.Count))
            return "Mẫu lịch lặp phải gồm các thứ hợp lệ, không trùng nhau.";
        var keys = request.RecurringWeekdayPatterns
            .Select(pattern => string.Join(',', pattern.OrderBy(day => day))).ToList();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
            return "Không được tạo hai mẫu lịch lặp giống nhau.";
        if (request.CourseDurationWeeks is < 1 or > 52)
            return "Thời lượng khóa học phải từ 1 đến 52 tuần.";
        var rawParts = (request.AllowedSessionsPerWeekCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allowed = MentorGroupPolicy.ParseSessionsPerWeekCsv(request.AllowedSessionsPerWeekCsv);
        if (rawParts.Length == 0 || allowed.Count != rawParts.Length || allowed.Count > 7
            || allowed.Any(x => x is < 1 or > 7) || allowed.Distinct().Count() != allowed.Count)
            return "Danh sách số buổi mỗi tuần phải gồm tối đa 7 giá trị khác nhau, mỗi giá trị từ 1 đến 7.";
        if (!allowed.Contains(request.DefaultSessionsPerWeek))
            return "Số buổi mỗi tuần mặc định phải nằm trong danh sách cường độ được phép.";
        if (request.CheckoutHoldMinutes is < 1 or > 60)
            return "Thời gian giữ chỗ khi thanh toán phải từ 1 đến 60 phút.";
        if (request.MinLearnersToStart < 1 || request.MinLearnersToStart > request.MaxLearners)
            return "Số học viên tối thiểu để mở lớp phải từ 1 đến sức chứa tối đa.";
        return null;
    }
}
