using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

internal static class MentorPurchaseFinalizer
{
    public static void MarkPaid(UserSubscription subscription, AppSettings settings, string? reference)
    {
        var now = DateTime.UtcNow;
        subscription.PaymentStatus = PaymentStatus.Paid;
        subscription.PaymentTransactionId = reference ?? subscription.PaymentTransactionId;
        subscription.StartDate = now;
        subscription.EndDate = subscription.SubscriptionPlan.DurationDays > 0 ? now.AddDays(subscription.SubscriptionPlan.DurationDays) : null;
        subscription.PaymentExpiresAt = null;
        foreach (var booking in subscription.MentorBookings)
        {
            if (booking.Status != MentorStatus.AwaitingPayment) continue;
            booking.Status = MentorStatus.Assigned;
            if (booking.MentorAvailability is not null)
                booking.MeetingLink = $"{settings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{booking.MentorAvailability.RoomId:D}/";
        }
    }
}
