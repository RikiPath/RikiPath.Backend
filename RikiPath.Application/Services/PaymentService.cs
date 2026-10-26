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
    AppSettings appSettings) : IPaymentService
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
                _ => await HandleMentorPaymentAsync((int)code, callback, webhook, ct) // code cũ của bạn
            };
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
            return ApiResponse<bool>.Fail("Không tìm thấy đơn mua gói Mentor Meeting.");
        if (callback.Amount != subscription.AmountPaid)
            return ApiResponse<bool>.Fail("Số tiền thanh toán không khớp với đơn hàng.");

        if (subscription.PaymentStatus == PaymentStatus.Paid)
            return ApiResponse<bool>.Success(true, message: "Webhook thanh toán đã được xử lý trước đó.");
        if (subscription.PaymentStatus != PaymentStatus.Pending)
            return ApiResponse<bool>.Success(true, message: "Đơn hàng không còn ở trạng thái chờ thanh toán.");

        if (!callback.IsSuccess)
        {
            subscription.PaymentStatus = PaymentStatus.Failed;
            foreach (var booking in subscription.MentorBookings ?? [])
            {
                if (booking.Status != MentorStatus.AwaitingPayment) continue;
                booking.Status = MentorStatus.Cancelled;
                if (booking.MentorAvailability is not null)
                {
                    booking.MentorAvailability.BookedCount = Math.Max(0, booking.MentorAvailability.BookedCount - 1);
                    subscription.MeetingSessionsUsed = Math.Max(0, subscription.MeetingSessionsUsed - 1);
                    booking.MentorAvailability = null;
                    booking.MentorAvailabilityId = null;
                }
            }
        }
        else
        {
            MentorPurchaseFinalizer.MarkPaid(subscription, appSettings, webhook.Data.Reference);
        }

        subscription.PaymentExpiresAt = null;
        await unitOfWork.SaveChangesAsync(ct);
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

        if (!callback.IsSuccess)
        {
            sub.PaymentStatus = PaymentStatus.Failed;
        }
        else
        {
            var now = DateTime.UtcNow;

            // Gia hạn: nếu đang còn hạn thì cộng nối tiếp từ EndDate, không phải từ "now"
            var currentEnd = await unitOfWork.UserSubscriptions
                .GetActiveEndDateAsync(sub.UserId, now, ct); // DateTime? của gói Paid còn hạn xa nhất

            var start = currentEnd is not null && currentEnd > now ? currentEnd.Value : now;

            sub.PaymentStatus = PaymentStatus.Paid;
            sub.PaymentTransactionId = webhook.Data.Reference;
            sub.StartDate = start;
            sub.EndDate = start.AddDays(sub.SubscriptionPlan.DurationDays);
            sub.AiGradingQuota = sub.SubscriptionPlan.AiGradingQuota;   // 3.000 / 6.500 / 10.000 / 2.000 điểm
            sub.AiGradingUsedCount = 0;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Success(true);
    }
}
