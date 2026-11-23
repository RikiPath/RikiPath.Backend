using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Payments;

namespace RikiPath.Application.IServices
{
    public interface IPaymentService
    {
        /// <summary>
        /// Xử lý webhook PayOS: xác minh chữ ký (trên đúng object <c>data</c> gốc khi có <paramref name="rawDataJson"/>),
        /// đối chiếu đơn hàng, rồi cập nhật trạng thái/kích hoạt quyền lợi đúng một lần.
        /// </summary>
        Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
            PayOsWebhookRequest webhook, CancellationToken cancellationToken, string? rawDataJson = null);
    }
}
