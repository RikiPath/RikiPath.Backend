using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Payments;

namespace RikiPath.Application.Services
{
    // LƯU Ý QUAN TRỌNG (cần bạn xác nhận lại):
    // 1) orderCode gửi cho PayOS = ConsultationPurchase.Id. PayOS yêu cầu orderCode là duy nhất
    //    cho mỗi LẦN tạo payment link - nếu learner bấm "thử lại thanh toán" cho cùng 1 purchase
    //    (link cũ hết hạn/huỷ), gọi CreatePaymentLinkAsync lần 2 với CÙNG orderCode có thể bị PayOS
    //    từ chối. Bản này chưa xử lý retry - nếu cần, phải đổi cách sinh orderCode (vd ghép thêm
    //    số lần thử) và có thể cần thêm cột riêng lưu orderCode thay vì dùng thẳng Purchase.Id.
    // 2) Giả định enum PaymentStatus có ít nhất 3 giá trị: Pending, Paid, Failed. Nếu enum thật
    //    của bạn đặt tên khác (vd Cancelled thay vì Failed), đổi lại ở HandlePayOsWebhookAsync.
    // 3) PayOS giới hạn "description" tối đa 25 ký tự - đã truncate ConsultationPackage.Name nếu
    //    dài hơn. Kiểm tra lại giới hạn thật trong doc PayOS hiện hành phòng khi họ đổi.
    public class PaymentService(IUnitOfWork unitOfWork, IPaymentGatewayClient paymentGatewayClient, IClaimService claimService)
        : IPaymentService
    {
        public async Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
            PayOsWebhookRequest webhook, CancellationToken cancellationToken)
        {
            try
            {
                var callback = paymentGatewayClient.VerifyWebhook(webhook);
                if (!callback.IsValidSignature)
                    return ApiResponse<bool>.Fail("Chữ ký webhook không hợp lệ.");


                await unitOfWork.SaveChangesAsync();

                return ApiResponse<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Lỗi xử lý webhook: {ex.Message}");
            }
        }
    }
}