using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Text;

namespace RikiPath.Application.Services
{
    public sealed class ContentApprovalNotifierService(
        IUnitOfWork unitOfWork,
        ILogger<ContentApprovalNotifierService> logger) : IContentApprovalNotifierService
    {
        public async Task NotifyAdminsOnRequestCreatedAsync(int requestCount, string batchName, CancellationToken cancellationToken)
        {
            try
            {
                // Lấy ID các Admin đang bật nhận thông báo
                var adminUserIds = await unitOfWork.UserAccounts.Query()
                    .Where(u => !u.IsDeleted && u.Role == Role.Admin && u.SystemNotificationsEnabled)
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);

                if (adminUserIds.Count == 0) return;

                var notifications = adminUserIds.Select(adminId => new Notification
                {
                    UserId = adminId,
                    Title = "Yêu cầu duyệt nội dung mới",
                    Message = $"Có {requestCount} nội dung mới trong lô '{batchName}' đang chờ bạn kiểm duyệt.",
                    Type = "CONTENT_APPROVAL_PENDING",
                    IsRead = false,
                    LinkUrl = "/admin/content-approvals", // Link dẫn Admin đến trang duyệt
                    CreatedDate = DateTime.UtcNow
                }).ToList();

                await unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi thông báo cho Admin về lô nội dung {BatchName}.", batchName);
            }
        }

        public async Task NotifyAuthorOnReviewResultAsync(int authorId, int totalRequested, List<(string ContentTitle, string RejectReason)> rejectedItems, CancellationToken cancellationToken)
        {
            try
            {
                var author = await unitOfWork.UserAccounts.GetByIdAsync(authorId, cancellationToken);
                if (author is null || !author.SystemNotificationsEnabled) return;

                Notification notification;
                var now = DateTime.UtcNow;

                // TRƯỜNG HỢP 1: DUYỆT THÀNH CÔNG TẤT CẢ
                if (rejectedItems == null || rejectedItems.Count == 0)
                {
                    notification = new Notification
                    {
                        UserId = authorId,
                        Title = "🎉 Nội dung đã được duyệt",
                        Message = $"Tuyệt vời! Toàn bộ {totalRequested} nội dung của bạn đã được duyệt thành công và lưu vào hệ thống.",
                        Type = "CONTENT_APPROVED_ALL",
                        IsRead = false,
                        LinkUrl = "/author-dashboard",
                        CreatedDate = now
                    };
                }
                // TRƯỜNG HỢP 2: CÓ NỘI DUNG BỊ TỪ CHỐI
                else
                {
                    var rejectedCount = rejectedItems.Count;
                    var approvedCount = totalRequested - rejectedCount;

                    var sb = new StringBuilder();
                    sb.AppendLine($"Đã duyệt {approvedCount}/{totalRequested} nội dung. Có {rejectedCount} nội dung bị từ chối:");

                    // Chỉ liệt kê tối đa 3 lỗi vào Message để tránh chuỗi quá dài
                    foreach (var item in rejectedItems.Take(3))
                    {
                        sb.AppendLine($"- {item.ContentTitle}: {item.RejectReason}");
                    }
                    if (rejectedCount > 3)
                    {
                        sb.AppendLine($"... và {rejectedCount - 3} nội dung khác (xem chi tiết).");
                    }

                    notification = new Notification
                    {
                        UserId = authorId,
                        Title = "⚠️ Kết quả kiểm duyệt nội dung",
                        Message = sb.ToString().TrimEnd(),
                        Type = "CONTENT_REJECTED_PARTIAL",
                        IsRead = false,
                        LinkUrl = "/author-dashboard",
                        CreatedDate = now
                    };
                }

                await unitOfWork.Notifications.AddAsync(notification, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi thông báo kết quả duyệt cho AuthorId: {AuthorId}.", authorId);
            }
        }
    }
}