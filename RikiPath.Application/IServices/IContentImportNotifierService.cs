namespace RikiPath.Application.IServices
{
    public interface IContentApprovalNotifierService
    {
        // 1. Gửi cho Admin khi Author tạo request thành công
        Task NotifyAdminsOnRequestCreatedAsync(int requestCount, string batchName, CancellationToken cancellationToken);

        // 2. Gửi cho Author khi Admin duyệt xong
        Task NotifyAuthorOnReviewResultAsync(int authorId, int totalRequested, List<(string ContentTitle, string RejectReason)> rejectedItems, CancellationToken cancellationToken);
    }
}
