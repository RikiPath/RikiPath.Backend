using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

internal static class MentorPurchaseFinalizer
{
    public static void MarkPaid(UserSubscription subscription, AppSettings settings, string? reference)
    {
        var now = DateTime.UtcNow;
        var holdExpired = subscription.PaymentExpiresAt is { } expiresAt && expiresAt <= now;
        subscription.PaymentStatus = PaymentStatus.Paid;
        subscription.PaymentTransactionId = reference ?? subscription.PaymentTransactionId;
        subscription.StartDate = now;
        var duration = subscription.MeetingSessionsIncluded switch { 1 => 14, 3 => 35, _ => subscription.SubscriptionPlan.DurationDays };
        subscription.EndDate = duration > 0 ? now.AddDays(duration) : null;
        subscription.PaymentExpiresAt = null;
        foreach (var booking in subscription.MentorBookings)
        {
            if (booking.Status != MentorStatus.AwaitingPayment) continue;
            if (holdExpired || booking.MentorAvailability is null || booking.MentorAvailability.StartTime <= now)
            {
                if (booking.MentorAvailability is { } slot)
                    slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
                booking.Status = MentorStatus.Cancelled;
                booking.MentorAvailability = null;
                booking.MentorAvailabilityId = null;
                subscription.MeetingSessionsUsed = Math.Max(0, subscription.MeetingSessionsUsed - 1);
                continue;
            }
            booking.Status = MentorStatus.Assigned;
            if (booking.MentorAvailability is not null)
                booking.MeetingLink = $"{settings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{booking.MentorAvailability.RoomId:D}/";
        }
    }
}
