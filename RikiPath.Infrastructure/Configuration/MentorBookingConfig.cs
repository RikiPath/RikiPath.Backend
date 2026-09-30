using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MentorBookingConfig : IEntityTypeConfiguration<MentorBooking>
    {
        public void Configure(EntityTypeBuilder<MentorBooking> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.MeetingLink).HasMaxLength(500);

            builder.HasOne(x => x.UserSubscription)
                .WithMany(x => x.MentorBookings)
                .HasForeignKey(x => x.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1-to-1, both sides optional: a request may not have booked a slot yet
            // (e.g. WrittenAnswer type never books one), and a slot may not be booked yet.
            builder.HasOne(x => x.MentorAvailability)
                .WithOne(x => x.MentorBooking)
                .HasForeignKey<MentorBooking>(x => x.MentorAvailabilityId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => new { x.UserSubscriptionId, x.Status });
            builder.HasIndex(x => x.MentorAvailabilityId).IsUnique();
        }
    }
}
