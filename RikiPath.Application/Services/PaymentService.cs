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
        PayOsWebhookRequest webhook, CancellationToken cancellationToken)
    {
        try
        {
            var callback = paymentGatewayClient.VerifyWebhook(webhook);
            if (!callback.IsValidSignature)
                return ApiResponse<bool>.Fail("Chữ ký webhook không hợp lệ.");
            if (callback.OrderCode <= 0 || callback.OrderCode > int.MaxValue)
                return ApiResponse<bool>.Fail("OrderCode không hợp lệ.");

            var subscription = await unitOfWork.UserSubscriptions
                .GetMeetingPurchaseWithBookingsAsync((int)callback.OrderCode, cancellationToken);
            if (subscription is null || subscription.MeetingSessionsIncluded <= 0)
                return ApiResponse<bool>.Fail("Không tìm thấy đơn mua gói Mentor Meeting.");
            if (callback.Amount != subscription.AmountPaid)
                return ApiResponse<bool>.Fail("Số tiền thanh toán không khớp với đơn hàng.");

            if (subscription.PaymentStatus == PaymentStatus.Paid)
                return ApiResponse<bool>.Success(true, message: "Webhook thanh toán đã được xử lý trước đó.");
            if (subscription.PaymentStatus != PaymentStatus.Pending)
                return ApiResponse<bool>.Success(true, message: "Đơn hàng không còn ở trạng thái chờ thanh toán.");

            var now = DateTime.UtcNow;
            if (!callback.IsSuccess)
            {
                subscription.PaymentStatus = PaymentStatus.Failed;
                foreach (var booking in subscription.MentorBookings ?? [])
                {
                    if (booking.Status != ConsultationStatus.AwaitingPayment) continue;
                    booking.Status = ConsultationStatus.Cancelled;
                    if (booking.MentorAvailability is not null)
                    {
                        booking.MentorAvailability.IsBooked = false;
                        booking.MentorAvailability = null;
                        booking.MentorAvailabilityId = null;
                    }
                }
            }
            else
            {
                subscription.PaymentStatus = PaymentStatus.Paid;
                subscription.PaymentTransactionId = webhook.Data.Reference ?? subscription.PaymentTransactionId;
                subscription.StartDate = now;
                subscription.EndDate = subscription.SubscriptionPlan.DurationDays > 0
                    ? now.AddDays(subscription.SubscriptionPlan.DurationDays)
                    : null;

                foreach (var booking in subscription.MentorBookings ?? [])
                {
                    if (booking.Status != ConsultationStatus.AwaitingPayment) continue;
                    booking.Status = ConsultationStatus.Assigned;
                    booking.RoomId = Guid.NewGuid();
                    booking.MeetingLink = BuildMeetingLink(booking.RoomId.Value);
                }
            }

            subscription.PaymentExpiresAt = null;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi xử lý webhook PayOS: {ex.Message}");
        }
    }

    private string BuildMeetingLink(Guid roomId)
    {
        var baseUrl = appSettings.FrontendBaseUrl;
        return $"{baseUrl.TrimEnd('/')}/mentor-meeting/{roomId:D}";
    }
}
