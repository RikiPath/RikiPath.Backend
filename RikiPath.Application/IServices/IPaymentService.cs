using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Payments;

namespace RikiPath.Application.IServices
{
    public interface IPaymentService
    {
        /// <summary>Xử lý webhook PayOS gọi về sau khi thanh toán - verify chữ ký rồi cập nhật
        /// PaymentStatus của ConsultationPurchase tương ứng.</summary>
        Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
            PayOsWebhookRequest webhook, CancellationToken cancellationToken);
    }
}