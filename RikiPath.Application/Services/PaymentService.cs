using Microsoft.EntityFrameworkCore;
using System.Net;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;
using RikiPath.Domain;

namespace RikiPath.Application.Services;

/// <summary>Processes PayOS callbacks and finalizes paid Mentor meeting packages.</summary>
public sealed class PaymentService(
    IUnitOfWork unitOfWork,
    IPaymentGatewayClient paymentGatewayClient,
    AppSettings appSettings,
    IMentorCreditLedgerService creditLedgerService) : IPaymentService
{
    public async Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
    PayOsWebhookRequest webhook, CancellationToken ct)
    {
        try
        {
            var callback = paymentGatewayClient.VerifyWebhook(webhook);
            if (!callback.IsValidSignature)
                return ApiResponse<bool>.Fail("Chữ ký webhook không hợp lệ.");

            var code = callback.OrderCode;
            if (code <= 0 || code > int.MaxValue)
                return ApiResponse<bool>.Fail("OrderCode không hợp lệ.");

            return code switch
            {
                >= 900_000_000 => await HandlePlanPaymentAsync((int)(code - 900_000_000), callback, webhook, ct),
                >= 800_000_000 => await HandleAiTopUpAsync((int)(code - 800_000_000), callback, webhook, ct),
                >= 700_000_000 => ApiResponse<bool>.Fail("Đơn khóa học cũ cần được đối soát thủ công."),
                _ => await HandleMentorPaymentAsync((int)code, callback, webhook, ct) // code cũ của bạn
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<bool>.Fail("Đơn thanh toán đang được cập nhật. Vui lòng gửi lại webhook.", HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi xử lý webhook PayOS: {ex.Message}");
        }
    }

    private async Task<ApiResponse<bool>> HandleAiTopUpAsync(
    int topUpId, PaymentCallbackResult callback, PayOsWebhookRequest webhook, CancellationToken ct)
    {
        var topUp = await unitOfWork.AiCreditTopUps.GetByIdAsync(topUpId, ct);
        if (topUp is null)
            return ApiResponse<bool>.Fail("Không tìm thấy đơn mua điểm AI.");
        if (callback.Amount != topUp.TotalAmount)
            return ApiResponse<bool>.Fail("Số tiền thanh toán không khớp.");

        if (topUp.PaymentStatus != PaymentStatus.Pending)
            return ApiResponse<bool>.Success(true, message: "Đơn đã được xử lý trước đó.");

        if (!callback.IsSuccess)
        {
            topUp.PaymentStatus = PaymentStatus.Failed;
        }
        else
        {
            topUp.PaymentStatus = PaymentStatus.Paid;
            topUp.PaymentTransactionId = webhook.Data.Reference;
            topUp.ActivatedAt = DateTime.UtcNow;

            // TODO: cộng topUp.Quantity điểm vào nơi bạn lưu số dư điểm AI của user
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }

    private async Task<ApiResponse<bool>> HandleMentorPaymentAsync(
    int orderCode, PaymentCallbackResult callback, PayOsWebhookRequest webhook, CancellationToken ct)
    {
        var subscription = await unitOfWork.UserSubscriptions
            .GetMeetingPurchaseWithBookingsAsync(orderCode, ct);
        if (subscription is null || subscription.MeetingSessionsIncluded <= 0)
            return ApiResponse<bool>.Fail("Không tìm thấy đơn mua gói tư vấn.");
        if (callback.Amount != subscription.AmountPaid)
            return ApiResponse<bool>.Fail("Số tiền thanh toán không khớp với đơn hàng.");

        if (subscription.PaymentStatus == PaymentStatus.Paid)
        {
            if (subscription.MeetingSessionsUsed < subscription.MeetingSessionsIncluded)
            {
                var existingGrant = await creditLedgerService.CreateGrantFromPaidSubscriptionAsync(orderCode, cancellationToken: ct);
                if (!existingGrant.IsSuccess)
                    return ApiResponse<bool>.Fail("Thanh toán đã xác nhận nhưng chưa cấp được lượt tư vấn.");
            }
            return ApiResponse<bool>.Success(true, message: "Webhook thanh toán đã được xử lý trước đó.");
        }
        if (subscription.PaymentStatus != PaymentStatus.Pending)
            return ApiResponse<bool>.Success(true, message: "Đơn hàng không còn ở trạng thái chờ thanh toán.");
        if (!string.IsNullOrWhiteSpace(subscription.PaymentTransactionId) &&
            !string.Equals(subscription.PaymentTransactionId, webhook.Data.PaymentLinkId, StringComparison.Ordinal))
            return ApiResponse<bool>.Fail("Mã liên kết thanh toán không khớp với đơn hàng.");

        if (!callback.IsSuccess)
        {
            MentorPurchaseFinalizer.MarkFailed(subscription);
        }
        else
        {
            MentorPurchaseFinalizer.MarkPaid(subscription, appSettings, webhook.Data.Reference);
        }

        await unitOfWork.SaveChangesAsync(ct);
        if (callback.IsSuccess && subscription.MeetingSessionsUsed < subscription.MeetingSessionsIncluded)
        {
            var grantResult = await creditLedgerService.CreateGrantFromPaidSubscriptionAsync(orderCode, cancellationToken: ct);
            if (!grantResult.IsSuccess)
                return ApiResponse<bool>.Fail("Thanh toán đã xác nhận nhưng chưa cấp được lượt tư vấn.");
        }
        return ApiResponse<bool>.Success(true);
    }

    private async Task<ApiResponse<bool>> HandlePlanPaymentAsync(
        int subscriptionId, PaymentCallbackResult callback, PayOsWebhookRequest webhook, CancellationToken ct)
    {
        // Include SubscriptionPlan
        var sub = await unitOfWork.UserSubscriptions.GetWithPlanAsync(subscriptionId, ct);
        if (sub is null)
            return ApiResponse<bool>.Fail("Không tìm thấy đơn mua gói.");
        if (callback.Amount != sub.AmountPaid)
            return ApiResponse<bool>.Fail("Số tiền thanh toán không khớp.");

        // Idempotent: PayOS có thể gọi lại nhiều lần
        if (sub.PaymentStatus != PaymentStatus.Pending)
            return ApiResponse<bool>.Success(true, message: "Đơn đã được xử lý trước đó.");

        if (!string.IsNullOrWhiteSpace(sub.PaymentTransactionId) &&
            !string.Equals(sub.PaymentTransactionId, webhook.Data.PaymentLinkId, StringComparison.Ordinal))
            return ApiResponse<bool>.Fail("Mã liên kết thanh toán không khớp với đơn hàng.");

        if (!callback.IsSuccess)
        {
            sub.PaymentStatus = PaymentStatus.Failed;
        }
        else
        {
            SubscriptionAccessService.Activate(sub, webhook.Data.Reference);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }
}
