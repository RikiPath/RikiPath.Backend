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

/// <summary>Processes PayOS callbacks for learner subscriptions, AI top-ups, and cohort courses.</summary>
public sealed class PaymentService(
    IUnitOfWork unitOfWork,
    IPaymentGatewayClient paymentGatewayClient,
    AppSettings appSettings,
    IStudyCourseService? studyCourses = null) : IPaymentService
{
    private readonly AppSettings _appSettings = appSettings;

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
                >= 700_000_000 => await HandleCoursePaymentAsync((int)(code - 700_000_000), callback, webhook.Data.Reference, ct),
                _ => ApiResponse<bool>.Fail("Gói tư vấn 1-1 đã ngừng hỗ trợ.", HttpStatusCode.Conflict, "INDIVIDUAL_MENTOR_PLAN_DISCONTINUED")
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

    private async Task<ApiResponse<bool>> HandleCoursePaymentAsync(int id, PaymentCallbackResult callback, string? reference, CancellationToken ct)
    {
        if (studyCourses is null) return ApiResponse<bool>.Fail("Dịch vụ khóa học chưa sẵn sàng.");
        var result = await studyCourses.ConfirmPaymentAsync(id, callback, reference, ct);
        return result.IsSuccess ? ApiResponse<bool>.Success(true)
            : ApiResponse<bool>.Fail(result.ErrorMessage ?? "Không xác nhận được thanh toán khóa học.", (System.Net.HttpStatusCode)result.StatusCode,
                result.ErrorCode);
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
        }

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
