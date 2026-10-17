using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

internal static class PublicContentQueryExtensions
{
    // Draft/PendingReview/Rejected nằm trong ContentApprovalRequests; bảng chính chỉ chứa nội dung đã duyệt.
    // Quy tắc Learner/Public vì vậy chỉ trả về bản ghi chưa bị xóa.
    public static IQueryable<T> WherePubliclyVisible<T>(this IQueryable<T> query)
        where T : Base
        => query.Where(item => !item.IsDeleted);
}
