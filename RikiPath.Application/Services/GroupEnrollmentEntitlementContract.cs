using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

/// <summary>
/// Kiểm tra quyền ghi danh lớp nhóm dựa trên sổ cái CohortEnrollmentEntitlement tách biệt.
/// Từ chối mặc định (fail-closed) khi thiếu UnitOfWork hoặc không có quyền Active hợp lệ cho lớp.
/// </summary>
public sealed class GroupEnrollmentEntitlementContract : IGroupEnrollmentEntitlementContract
{
    private readonly IUnitOfWork? _uow;

    public GroupEnrollmentEntitlementContract()
    {
    }

    [ActivatorUtilitiesConstructor]
    public GroupEnrollmentEntitlementContract(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private static GroupEnrollmentEntitlementDecision FailClosed(string message) => new(
        IsAuthorized: false,
        ErrorMessage: message,
        RequiresCheckout: true);

    public GroupEnrollmentEntitlementDecision EvaluateGroupEnrollment(
        StudyCohort cohort,
        StudyCoursePlan plan,
        int learnerId,
        string? entitlementReference)
    {
        if (_uow?.CohortEnrollmentEntitlements is null)
        {
            return FailClosed("Ghi danh lớp nhóm mới đang chờ phê duyệt quyền học và thanh toán; chưa tạo giao dịch.");
        }

        var normalizedRef = string.IsNullOrWhiteSpace(entitlementReference) ? null : entitlementReference.Trim();
        if (normalizedRef is not null &&
            (normalizedRef.StartsWith("CREDIT", StringComparison.OrdinalIgnoreCase)
             || normalizedRef.Contains("CONSULTATION", StringComparison.OrdinalIgnoreCase)
             || normalizedRef.Contains("1ON1", StringComparison.OrdinalIgnoreCase)))
        {
            return FailClosed("Vé tư vấn 1-1 không thể dùng hoặc chuyển đổi sang quyền ghi danh lớp nhóm.");
        }

        var now = DateTime.UtcNow;
        var match = _uow.CohortEnrollmentEntitlements.Query()
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.StudyCohortId == cohort.Id
                && x.LearnerId == learnerId
                && x.Status == "Active"
                && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)
                && (x.PlanId == null || x.PlanId == plan.Id)
                && x.SourceType != "ConsultationCredit"
                && x.SourceType != "Credit1on1"
                && x.SourceType != "OneOnOneCredit")
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();

        if (match is null)
        {
            return FailClosed("Bạn chưa có quyền ghi danh lớp nhóm cho lớp này. Vé tư vấn 1-1 không tự chuyển đổi thành quyền học nhóm.");
        }

        if (normalizedRef is not null
            && !string.Equals(match.EntitlementReference, normalizedRef, StringComparison.Ordinal))
        {
            return FailClosed("Mã quyền ghi danh lớp nhóm không khớp với quyền đang hoạt động của bạn.");
        }

        return new GroupEnrollmentEntitlementDecision(
            IsAuthorized: true,
            ErrorMessage: null,
            RequiresCheckout: false,
            EntitlementReference: match.EntitlementReference);
    }
}
