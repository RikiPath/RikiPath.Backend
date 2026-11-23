using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// <remarks>
/// Response policy (PayOS retries any non-2xx webhook):
/// forged/unsigned → 400 (nothing changes); a signed event that can never apply here (unknown order, amount or link
/// mismatch, PayOS's own confirm-webhook test) → 200 acknowledged with no grant, logged for review;
/// a transient failure (database/concurrency) → 5xx so PayOS retries — the order is never marked done on a failed write.
/// </remarks>
public sealed class PaymentService(
    IUnitOfWork unitOfWork,
    IPaymentGatewayClient paymentGatewayClient,
    AppSettings appSettings,
    IStudyCourseService? studyCourses = null,
    ILogger<PaymentService>? logger = null) : IPaymentService
{
    private readonly AppSettings _appSettings = appSettings;

    public async Task<ApiResponse<bool>> HandlePayOsWebhookAsync(
        PayOsWebhookRequest webhook, CancellationToken ct, string? rawDataJson = null)
    {
        try
        {
            if (webhook?.Data is null)
                return ApiResponse<bool>.Fail("Webhook không hợp lệ.", HttpStatusCode.BadRequest, "WEBHOOK_INVALID");

            var callback = paymentGatewayClient.VerifyWebhook(webhook, rawDataJson);
            if (!callback.IsValidSignature)
            {
                logger?.LogWarning("PayOS webhook rejected: invalid signature. OrderCode={OrderCode}", webhook.Data.OrderCode);
                return ApiResponse<bool>.Fail("Chữ ký webhook không hợp lệ.", HttpStatusCode.BadRequest, "WEBHOOK_SIGNATURE_INVALID");
            }

            var code = callback.OrderCode;
            if (code >= StudyOrderCodes.V2Base)
            {
                return StudyOrderCodes.TryGetPurchaseId(code, out var v2Id)
                    ? await HandlePlanPaymentAsync(v2Id, callback, webhook, ct)
                    : Ignored(code, "order code is not a RikiPath study order");
            }
            if (code <= 0 || code > int.MaxValue)
                return Ignored(code, "order code out of range");

            return code switch
            {
                >= 900_000_000 => await HandlePlanPaymentAsync((int)(code - 900_000_000), callback, webhook, ct),
                >= 800_000_000 => await HandleAiTopUpAsync((int)(code - 800_000_000), callback, webhook, ct),
                >= 700_000_000 => await HandleCoursePaymentAsync((int)(code - 700_000_000), callback, webhook.Data.Reference, ct),
                // PayOS "confirm webhook" sends a signed sample (e.g. orderCode 123) and requires 2xx; individual mentor
                // plans are discontinued. Neither grants anything.
                _ => Ignored(code, "no RikiPath order uses this code range"),
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<bool>.Fail("Đơn thanh toán đang được cập nhật. Vui lòng gửi lại webhook.", HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "PayOS webhook processing failed; PayOS will retry. OrderCode={OrderCode}", webhook?.Data?.OrderCode);
            return ApiResponse<bool>.Fail("Không xử lý được webhook PayOS. Vui lòng gửi lại.", HttpStatusCode.InternalServerError, "WEBHOOK_PROCESSING_FAILED");
        }
    }

    private ApiResponse<bool> Ignored(long orderCode, string reason)
    {
        logger?.LogInformation("PayOS webhook acknowledged without changes. OrderCode={OrderCode} Reason={Reason}", orderCode, reason);
        return ApiResponse<bool>.Success(false, message: "Webhook đã nhận; không có đơn hàng phù hợp.");
    }

    private ApiResponse<bool> Rejected(int purchaseId, long orderCode, string reason)
    {
        logger?.LogError("PayOS webhook did not match the order; no entitlement granted. PurchaseId={PurchaseId} OrderCode={OrderCode} Reason={Reason}",
            purchaseId, orderCode, reason);
        return ApiResponse<bool>.Success(false, message: "Webhook đã nhận; thông tin không khớp đơn hàng nên không được ghi nhận.");
    }

    private async Task<ApiResponse<bool>> HandleCoursePaymentAsync(int id, PaymentCallbackResult callback, string? reference, CancellationToken ct)
    {
        if (studyCourses is null) return ApiResponse<bool>.Fail("Dịch vụ khóa học chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
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
            return Ignored(callback.OrderCode, "unknown AI top-up");
        if (callback.Amount != topUp.TotalAmount)
            return Rejected(topUpId, callback.OrderCode, "amount mismatch (AI top-up)");

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
        var data = webhook.Data;
        var lookup = await unitOfWork.UserSubscriptions.Query()
            .AsNoTracking()
            .Where(s => s.Id == subscriptionId && !s.IsDeleted)
            .Select(s => new { s.UserId })
            .FirstOrDefaultAsync(ct);
        if (lookup is null)
            return Ignored(callback.OrderCode, "unknown study purchase");

        ApiResponse<bool>? response = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            response = null;
            // Same learner lock as checkout and reconciliation: a webhook, its retry and a status poll are applied one at a time.
            await unitOfWork.UserAccounts.GetByIdForUpdateAsync(lookup.UserId, token);
            var sub = await unitOfWork.UserSubscriptions.GetWithPlanAsync(subscriptionId, token);
            if (sub is null)
            {
                response = Ignored(callback.OrderCode, "unknown study purchase");
                return;
            }

            // Idempotent: PayOS có thể gọi lại nhiều lần — một giao dịch chỉ kích hoạt quyền lợi một lần.
            if (sub.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                response = ApiResponse<bool>.Success(true, message: "Đơn đã được xử lý trước đó.");
                return;
            }

            if (callback.Amount != sub.AmountPaid)
            {
                response = Rejected(sub.Id, callback.OrderCode, "amount mismatch");
                return;
            }
            if (!string.Equals(data.Currency, "VND", StringComparison.OrdinalIgnoreCase))
            {
                response = Rejected(sub.Id, callback.OrderCode, "currency mismatch");
                return;
            }
            // The link id is the binding between this order and the PayOS transaction; the order code alone is not
            // trusted (other environments sharing the channel reuse the same purchase ids).
            if (!StudyOrderCodes.TryReadPendingReference(sub.PaymentTransactionId, sub.Id, out var boundLinkId, out var boundOrderCode) ||
                !string.Equals(boundLinkId, data.PaymentLinkId, StringComparison.Ordinal))
            {
                response = Rejected(sub.Id, callback.OrderCode, "payment link id mismatch");
                return;
            }
            if (callback.OrderCode != boundOrderCode)
            {
                response = Rejected(sub.Id, callback.OrderCode, "order code mismatch");
                return;
            }

            if (!callback.IsSuccess)
            {
                // Non-success events do not close the order; the status poll confirms CANCELLED/EXPIRED with PayOS.
                response = ApiResponse<bool>.Success(true, message: "Đã ghi nhận trạng thái chưa thanh toán.");
                return;
            }

            // A verified PAID event also closes an order that was locally marked Failed/Cancelled/Expired: the money was taken.
            var previous = sub.PaymentStatus;
            var category = SubscriptionAccessService.OrderCategoryPublic(sub);
            var stackFrom = await SubscriptionAccessService.FindActiveCategoryExpiryAsync(
                unitOfWork, sub.UserId, category, sub.Id, DateTime.UtcNow, token);
            SubscriptionAccessService.Activate(sub, data.Reference, stackFrom);

            await unitOfWork.SaveChangesAsync(token);
            logger?.LogInformation("Study plan activated by PayOS webhook. PurchaseId={PurchaseId} PlanId={PlanId} OrderCode={OrderCode} PreviousStatus={PreviousStatus}",
                sub.Id, sub.SubscriptionPlanId, callback.OrderCode, previous);
            response = ApiResponse<bool>.Success(true);
        }, ct);

        return response ?? ApiResponse<bool>.Fail("Không thể xử lý đơn mua gói.", HttpStatusCode.InternalServerError);
    }
}
