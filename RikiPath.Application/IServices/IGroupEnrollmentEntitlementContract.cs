using RikiPath.Domain.Entities;

namespace RikiPath.Application.IServices;

public sealed record GroupEnrollmentEntitlementDecision(
    bool IsAuthorized,
    string? ErrorMessage = null,
    bool RequiresCheckout = true,
    string? EntitlementReference = null);

/// <summary>
/// Ranh giới quyền ghi danh lớp nhóm tách biệt hoàn toàn với vé tư vấn 1-1.
/// Fail-closed mặc định khi học viên chưa có quyền ghi danh hợp lệ cho lớp nhóm tương ứng.
/// </summary>
public interface IGroupEnrollmentEntitlementContract
{
    GroupEnrollmentEntitlementDecision EvaluateGroupEnrollment(
        StudyCohort cohort,
        StudyCoursePlan plan,
        int learnerId,
        string? entitlementReference);
}
