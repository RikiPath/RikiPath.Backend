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
        var duration = DurationDays(subscription);
        subscription.EndDate = duration > 0 ? now.AddDays(duration) : null;
        subscription.PaymentExpiresAt = null;
        foreach (var booking in subscription.MentorBookings)
        {
            if (booking.Status != MentorStatus.AwaitingPayment) continue;
            booking.Status = MentorStatus.Accepted;
            if (booking.MentorAvailability is not null)
                booking.MeetingLink = $"{settings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{booking.MentorAvailability.RoomId:D}/";
        }
    }

    internal static int DurationDays(UserSubscription subscription)
        => subscription.MeetingSessionsIncluded switch { 1 => 30, 3 => 90, _ => subscription.SubscriptionPlan.DurationDays };
}
