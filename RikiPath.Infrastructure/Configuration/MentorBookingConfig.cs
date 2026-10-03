using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
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
            builder.HasIndex(x => x.RoomId).IsUnique().HasFilter("\"RoomId\" IS NOT NULL");

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

            builder.HasData(new MentorBooking
            {
                Id = 990001,
                UserSubscriptionId = 990001,
                MentorAvailabilityId = 990001,
                Status = MentorStatus.Assigned,
                Question = "Meeting demo để kiểm thử phòng 1-1 trên frontend.",
                ScheduledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                MeetingLink = "http://localhost:5173/mentor-meeting/5d75c4cc-5404-4f9a-91e8-b8596f31d2a2",
                RoomId = Guid.Parse("5d75c4cc-5404-4f9a-91e8-b8596f31d2a2"),
                IsDeleted = false,
                CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
            });
        }
    }
}
