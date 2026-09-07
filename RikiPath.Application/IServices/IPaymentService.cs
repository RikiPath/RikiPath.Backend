using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Payments;

namespace RikiPath.Application.IServices
{
    public interface IPaymentService
    {
        /// <summary>Tạo ConsultationPurchase (status Pending) + gọi PayOS tạo payment link.</summary>
        Task<ApiResponse<ConsultationPaymentResponse>> CreateConsultationPaymentAsync(
            int userId, CreateConsultationPaymentRequest request, CancellationToken cancellationToken);

        /// <summary>Xử lý webhook PayOS gọi về sau khi thanh toán - verify chữ ký rồi cập nhật
        /// PaymentStatus của ConsultationPurchase tương ứng.</summary>
        Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
            PayOsWebhookRequest webhook, CancellationToken cancellationToken);

        /// <summary>Learner tự kiểm tra trạng thái giao dịch của mình (polling từ FE trong lúc
        /// chờ webhook, hoặc xem lại lịch sử).</summary>
        Task<ApiResponse<ConsultationPurchaseStatusResponse>> GetPurchaseStatusAsync(
            int userId, int purchaseId, CancellationToken cancellationToken);
    }
}