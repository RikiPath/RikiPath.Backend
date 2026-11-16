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
        if (subscription.PendingMentorAvailability is { } reservedSlot &&
            !holdExpired && IsBookable(reservedSlot, now))
        {
            subscription.MentorBookings.Add(new MentorBooking
            {
                UserSubscription = subscription,
                MentorAvailability = reservedSlot,
                Status = MentorStatus.Accepted,
                ScheduledAt = reservedSlot.StartTime,
                Question = subscription.PendingMentorQuestion,
                MeetingLink = MeetingLink(settings, reservedSlot)
            });
            subscription.MeetingSessionsUsed++;
            // Include the reserved slot's row version when confirming the booking.
            reservedSlot.ModifiedDate = now;
            ClearReservation(subscription);
        }
        else
        {
            ReleaseReservation(subscription);
        }

        subscription.PaymentStatus = PaymentStatus.Paid;
        subscription.PaymentTransactionId = reference ?? subscription.PaymentTransactionId;
        subscription.StartDate = now;
        var duration = DurationDays(subscription);
        subscription.EndDate = duration > 0 ? now.AddDays(duration) : null;
        subscription.PaymentExpiresAt = null;
        foreach (var booking in subscription.MentorBookings)
        {
            if (booking.Status != MentorStatus.AwaitingPayment) continue;
            if (holdExpired || booking.MentorAvailability is not { } slot || !IsBookable(slot, now))
            {
                ReleaseLegacyBooking(booking);
                continue;
            }
            booking.Status = MentorStatus.Accepted;
            booking.MeetingLink = MeetingLink(settings, slot);
            slot.ModifiedDate = now;
        }
    }

    internal static void MarkFailed(UserSubscription subscription)
    {
        ReleaseReservation(subscription);
        foreach (var booking in subscription.MentorBookings.Where(x => x.Status == MentorStatus.AwaitingPayment))
            ReleaseLegacyBooking(booking);
        subscription.PaymentStatus = PaymentStatus.Failed;
        subscription.PaymentExpiresAt = null;
    }

    internal static void ReleaseReservation(UserSubscription subscription)
    {
        if (subscription.PendingMentorAvailability is { } slot)
            slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
        ClearReservation(subscription);
    }

    internal static void ReleaseLegacyBooking(MentorBooking booking)
    {
        if (booking.MentorAvailability is { } slot)
            slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
        booking.Status = MentorStatus.Cancelled;
        booking.MentorAvailability = null;
        booking.MentorAvailabilityId = null;
        var subscription = booking.UserSubscription;
        subscription.MeetingSessionsUsed = Math.Max(0, subscription.MeetingSessionsUsed - 1);
    }

    private static void ClearReservation(UserSubscription subscription)
    {
        subscription.PendingMentorAvailability = null;
        subscription.PendingMentorAvailabilityId = null;
        subscription.PendingMentorQuestion = null;
    }

    private static bool IsBookable(MentorAvailability slot, DateTime now)
        => !slot.IsDeleted && slot.IsApproved && slot.RejectedAt == null && slot.StartTime > now;

    private static string MeetingLink(AppSettings settings, MentorAvailability slot)
        => $"{settings.FrontendBaseUrl.TrimEnd('/')}/consultation-room/{slot.RoomId:D}/";

    internal static int DurationDays(UserSubscription subscription)
        => subscription.MeetingSessionsIncluded switch { 1 => 30, 3 => 90, _ => subscription.SubscriptionPlan.DurationDays };
}
