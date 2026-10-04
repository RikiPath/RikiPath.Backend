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

            builder.HasOne(x => x.UserSubscription)
                .WithMany(x => x.MentorBookings)
                .HasForeignKey(x => x.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.MentorAvailability)
                .WithMany(x => x.MentorBookings)
                .HasForeignKey(x => x.MentorAvailabilityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserSubscriptionId, x.Status });

            builder.HasIndex(x => new { x.MentorAvailabilityId, x.UserSubscriptionId }).IsUnique();

            builder.HasData(
               new MentorBooking
               {
                   Id = 990001,
                   UserSubscriptionId = 990001,
                   MentorAvailabilityId = 990001,
                   Status = MentorStatus.Assigned,
                   Question = "Meeting demo để kiểm thử phòng họp trên frontend.",
                   ScheduledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                   MeetingLink = "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001",
                   IsDeleted = false,
                   CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
               },
               new MentorBooking
               {
                   Id = 990002,
                   UserSubscriptionId = 990002,
                   MentorAvailabilityId = 990001,
                   Status = MentorStatus.Assigned,
                   Question = "Em muốn nhờ Mentor giải đáp phần ngữ pháp N3 bài 5.",
                   ScheduledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                   MeetingLink = "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001",
                   IsDeleted = false,
                   CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
               },
               new MentorBooking
               {
                   Id = 990003,
                   UserSubscriptionId = 990003,
                   MentorAvailabilityId = 990001,
                   Status = MentorStatus.Assigned,
                   Question = "Hỏi về cách phân biệt giữa ~てくる và ~ていく.",
                   ScheduledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                   MeetingLink = "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001",
                   IsDeleted = false,
                   CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
               },
               new MentorBooking
               {
                   Id = 990004,
                   UserSubscriptionId = 990004,
                   MentorAvailabilityId = 990001,
                   Status = MentorStatus.Assigned,
                   Question = "Tư vấn phương pháp luyện nghe JLPT N3 hiệu quả.",
                   ScheduledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                   MeetingLink = "http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001",
                   IsDeleted = false,
                   CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
               }
           );
        }
    }
}