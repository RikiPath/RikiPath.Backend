using RikiPath.Domain.Entities;
using Domain.Enums;
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
    public class PaymentService(IUnitOfWork unitOfWork, IPaymentGatewayClient paymentGatewayClient)
        : IPaymentService
    {
        private const int MaxDescriptionLength = 25;

        public async Task<ApiResponse<ConsultationPaymentResponse>> CreateConsultationPaymentAsync(
            int userId, CreateConsultationPaymentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var package = await unitOfWork.ConsultationPackages.GetByIdAsync(request.ConsultationPackageId);
                if (package is null || !package.IsActive)
                    return ApiResponse<ConsultationPaymentResponse>.Fail("Gói tư vấn không tồn tại hoặc đã ngừng bán.");

                var amount = (int)Math.Round(package.Price, MidpointRounding.AwayFromZero);
                if (amount <= 0)
                    return ApiResponse<ConsultationPaymentResponse>.Fail("Giá gói tư vấn không hợp lệ.");

                var purchase = new ConsultationPurchase
                {
                    UserId = userId,
                    ConsultationPackageId = package.Id,
                    AmountPaid = package.Price,
                    PaymentStatus = PaymentStatus.Pending,
                    PurchasedAt = DateTime.UtcNow
                };

                await unitOfWork.ConsultationPurchases.AddAsync(purchase);
                await unitOfWork.SaveChangesAsync();
                // Save trước để purchase.Id được DB sinh ra (identity) - dùng làm orderCode gửi PayOS.

                var orderCode = purchase.Id;
                var description = package.Name.Length > MaxDescriptionLength
                    ? package.Name[..MaxDescriptionLength]
                    : package.Name;

                var link = await paymentGatewayClient.CreatePaymentLinkAsync(
                    orderCode, amount, description, request.ReturnUrl, request.CancelUrl, cancellationToken);

                purchase.PaymentTransactionId = link.PaymentLinkId;
                unitOfWork.ConsultationPurchases.Update(purchase);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationPaymentResponse>.Success(
                    new ConsultationPaymentResponse
                    {
                        PurchaseId = purchase.Id,
                        OrderCode = orderCode,
                        CheckoutUrl = link.CheckoutUrl,
                        PaymentLinkId = link.PaymentLinkId,
                        QrCode = link.QrCode,
                        Amount = package.Price
                    });
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationPaymentResponse>.Fail($"Không thể tạo thanh toán: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
            PayOsWebhookRequest webhook, CancellationToken cancellationToken)
        {
            try
            {
                var callback = paymentGatewayClient.VerifyWebhook(webhook);
                if (!callback.IsValidSignature)
                    return ApiResponse<bool>.Fail("Chữ ký webhook không hợp lệ.");

                var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync((int)callback.OrderCode);
                if (purchase is null)
                    return ApiResponse<bool>.Fail("Không tìm thấy giao dịch tương ứng với orderCode.");

                // Idempotent: PayOS có thể gọi webhook nhiều lần cho cùng 1 giao dịch.
                if (purchase.PaymentStatus == PaymentStatus.Paid)
                    return ApiResponse<bool>.Success(true);

                purchase.PaymentStatus = callback.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed;
                unitOfWork.ConsultationPurchases.Update(purchase);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Lỗi xử lý webhook: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationPurchaseStatusResponse>> GetPurchaseStatusAsync(
            int userId, int purchaseId, CancellationToken cancellationToken)
        {
            try
            {
                var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync(purchaseId);
                if (purchase is null || purchase.UserId != userId)
                    return ApiResponse<ConsultationPurchaseStatusResponse>.Fail("Không tìm thấy giao dịch.");

                var package = await unitOfWork.ConsultationPackages.GetByIdAsync(purchase.ConsultationPackageId);

                return ApiResponse<ConsultationPurchaseStatusResponse>.Success(
                    new ConsultationPurchaseStatusResponse
                    {
                        Id = purchase.Id,
                        ConsultationPackageId = purchase.ConsultationPackageId,
                        PackageName = package?.Name ?? "N/A",
                        AmountPaid = purchase.AmountPaid,
                        Status = purchase.PaymentStatus,
                        PurchasedAt = purchase.PurchasedAt
                    });
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationPurchaseStatusResponse>.Fail($"Lỗi khi lấy trạng thái giao dịch: {ex.Message}");
            }
        }
    }
}