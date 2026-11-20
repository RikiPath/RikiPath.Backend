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
            builder.Property(x => x.Version).IsRowVersion();

            builder.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);
            builder.Property(x => x.MeetingSessionsIncluded).HasDefaultValue(0);
            builder.Property(x => x.MeetingSessionsUsed).HasDefaultValue(0);
            builder.Property(x => x.MentorReviewPlan).HasMaxLength(6000);
            builder.Property(x => x.PendingMentorQuestion).HasMaxLength(2000);
            builder.Property(x => x.PlanNameSnapshot).HasMaxLength(150);
            builder.Property(x => x.PlanCategorySnapshot).HasMaxLength(32);

            builder.HasOne(x => x.UserAccount)
                .WithMany(x => x.UserSubscriptions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SubscriptionPlan)
                .WithMany(x => x.UserSubscriptions)
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Mentor)
                .WithMany()
                .HasForeignKey(x => x.MentorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.CertificateLevel)
                .WithMany()
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.PendingMentorAvailability)
                .WithMany()
                .HasForeignKey(x => x.PendingMentorAvailabilityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.PaymentStatus, x.EndDate });

            // Learner 900006 is the full-access demo account. It owns the longest
            // self-study plan, a learning-path AI credit, and the legacy Mentor plan.
            // Learners 900007 -> 900009 keep the Mentor-only demo subscription.
            builder.HasData(
                new UserSubscription
                {
                    Id = 990001,
                    UserId = 900006,
                    SubscriptionPlanId = 7,
                    AmountPaid = 459000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-MENTOR-12-SESSIONS",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Unspecified),
                    DurationDaysSnapshot = 90,
                    PlanNameSnapshot = "Meeting 1-1 với Mentor - 12 buổi",
                    PlanCategorySnapshot = "mentor_legacy",
                    PlanRevisionSnapshot = 1,
                    MeetingSessionsIncluded = 12,
                    MeetingSessionsUsed = 0,
                    PurchasedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                },
                new UserSubscription
                {
                    Id = 990002,
                    UserId = 900007,
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
                    UserId = 900008,
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
                    UserId = 900009,
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
                },
                new UserSubscription
                {
                    Id = 990005,
                    UserId = 900006,
                    SubscriptionPlanId = 6,
                    AmountPaid = 3000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-FULL-ACCESS-SELF-STUDY",
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    EndDate = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Unspecified),
                    AiGradingQuota = 15,
                    AiGradingUsedCount = 0,
                    DurationDaysSnapshot = 90,
                    PlanNameSnapshot = "Tự học tiếng Nhật - 3 tháng",
                    PlanCategorySnapshot = "self_study",
                    PlanRevisionSnapshot = 1,
                    PurchasedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                },
                new UserSubscription
                {
                    Id = 990006,
                    UserId = 900006,
                    SubscriptionPlanId = 8104,
                    AmountPaid = 1000m,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentTransactionId = "DEMO-FULL-ACCESS-LEARNING-PATH",
                    AiGradingQuota = 1,
                    AiGradingUsedCount = 0,
                    DurationDaysSnapshot = 0,
                    PlanNameSnapshot = "Mua 1 lượt AI đề xuất lộ trình",
                    PlanCategorySnapshot = "learning_path",
                    PlanRevisionSnapshot = 1,
                    PurchasedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    IsDeleted = false
                }
            );
        }
    }
}
