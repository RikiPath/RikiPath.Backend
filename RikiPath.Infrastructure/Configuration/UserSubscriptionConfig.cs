using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration
{
    public class UserSubscriptionConfig : IEntityTypeConfiguration<UserSubscription>
    {
        public void Configure(EntityTypeBuilder<UserSubscription> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);
            builder.Property(x => x.MeetingSessionsIncluded).HasDefaultValue(0);
            builder.Property(x => x.MeetingSessionsUsed).HasDefaultValue(0);

            builder.HasOne(x => x.UserAccount)
                .WithMany(x => x.UserSubscriptions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SubscriptionPlan)
                .WithMany(x => x.UserSubscriptions)
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.PaymentStatus, x.EndDate });

            // Paid demo subscription lets the seeded Learner test included Mentor sessions without PayOS checkout.
            builder.HasData(
                new UserSubscription
                {
                    Id = 990001,
                    UserId = 900007,
                    SubscriptionPlanId = 7,
                    AmountPaid = 459000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-MENTOR-12-SESSIONS",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2026, 12, 30, 23, 59, 59, DateTimeKind.Unspecified),
                    MeetingSessionsIncluded = 12,
                    MeetingSessionsUsed = 1,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                },
                new UserSubscription
                {
                    Id = 990002,
                    UserId = 900008,
                    SubscriptionPlanId = 7,
                    AmountPaid = 459000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-MENTOR-12-SESSIONS-2",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2026, 12, 30, 23, 59, 59, DateTimeKind.Unspecified),
                    MeetingSessionsIncluded = 12,
                    MeetingSessionsUsed = 1,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                },
                new UserSubscription
                {
                    Id = 990003,
                    UserId = 900009,
                    SubscriptionPlanId = 7,
                    AmountPaid = 459000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-MENTOR-12-SESSIONS-3",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2026, 12, 30, 23, 59, 59, DateTimeKind.Unspecified),
                    MeetingSessionsIncluded = 12,
                    MeetingSessionsUsed = 1,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                },
                new UserSubscription
                {
                    Id = 990004,
                    UserId = 900010,
                    SubscriptionPlanId = 7,
                    AmountPaid = 459000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-MENTOR-12-SESSIONS-4",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2026, 12, 30, 23, 59, 59, DateTimeKind.Unspecified),
                    MeetingSessionsIncluded = 12,
                    MeetingSessionsUsed = 1,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                }
            );
        }
    }
}
