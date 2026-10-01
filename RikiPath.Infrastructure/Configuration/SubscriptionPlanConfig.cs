using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class SubscriptionPlanConfig : IEntityTypeConfiguration<SubscriptionPlan>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Price).HasColumnType("decimal(18,0)");
            builder.Property(x => x.MeetingSessionCount).HasDefaultValue(0);
            builder.HasMany(x => x.Features)
                .WithMany(x => x.SubscriptionPlans)
                .UsingEntity<Dictionary<string, object>>(
                    "FeatureSubscriptionPlan",
                    feature => feature
                        .HasOne<Feature>()
                        .WithMany()
                        .HasForeignKey("FeaturesId")
                        .OnDelete(DeleteBehavior.Cascade),
                    plan => plan
                        .HasOne<SubscriptionPlan>()
                        .WithMany()
                        .HasForeignKey("SubscriptionPlansId")
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.HasKey("FeaturesId", "SubscriptionPlansId");
                        join.HasData(
                            new { FeaturesId = 1, SubscriptionPlansId = 1 }, new { FeaturesId = 2, SubscriptionPlansId = 1 }, new { FeaturesId = 3, SubscriptionPlansId = 1 }, new { FeaturesId = 4, SubscriptionPlansId = 1 }, new { FeaturesId = 5, SubscriptionPlansId = 1 }, new { FeaturesId = 6, SubscriptionPlansId = 1 },
                            new { FeaturesId = 1, SubscriptionPlansId = 2 }, new { FeaturesId = 2, SubscriptionPlansId = 2 }, new { FeaturesId = 3, SubscriptionPlansId = 2 }, new { FeaturesId = 4, SubscriptionPlansId = 2 }, new { FeaturesId = 5, SubscriptionPlansId = 2 }, new { FeaturesId = 6, SubscriptionPlansId = 2 },
                            new { FeaturesId = 1, SubscriptionPlansId = 3 }, new { FeaturesId = 2, SubscriptionPlansId = 3 }, new { FeaturesId = 3, SubscriptionPlansId = 3 }, new { FeaturesId = 4, SubscriptionPlansId = 3 }, new { FeaturesId = 5, SubscriptionPlansId = 3 }, new { FeaturesId = 6, SubscriptionPlansId = 3 },
                            new { FeaturesId = 1, SubscriptionPlansId = 4 }, new { FeaturesId = 2, SubscriptionPlansId = 4 }, new { FeaturesId = 3, SubscriptionPlansId = 4 }, new { FeaturesId = 4, SubscriptionPlansId = 4 }, new { FeaturesId = 5, SubscriptionPlansId = 4 }, new { FeaturesId = 6, SubscriptionPlansId = 4 },
                            new { FeaturesId = 1, SubscriptionPlansId = 5 }, new { FeaturesId = 2, SubscriptionPlansId = 5 }, new { FeaturesId = 3, SubscriptionPlansId = 5 }, new { FeaturesId = 4, SubscriptionPlansId = 5 }, new { FeaturesId = 5, SubscriptionPlansId = 5 }, new { FeaturesId = 6, SubscriptionPlansId = 5 },
                            new { FeaturesId = 1, SubscriptionPlansId = 6 }, new { FeaturesId = 2, SubscriptionPlansId = 6 }, new { FeaturesId = 3, SubscriptionPlansId = 6 }, new { FeaturesId = 4, SubscriptionPlansId = 6 }, new { FeaturesId = 5, SubscriptionPlansId = 6 }, new { FeaturesId = 6, SubscriptionPlansId = 6 });
                    });

            builder.HasData(
                new SubscriptionPlan { Id = 1, Name = "Tự học tiếng Nhật - 1 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tuần.", DurationDays = 7, Price = 79000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 6, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 2, Name = "Tự học tiếng Nhật - 2 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tuần.", DurationDays = 14, Price = 119000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 5, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 3, Name = "Tự học tiếng Nhật - 3 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tuần.", DurationDays = 21, Price = 159000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 4, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 4, Name = "Tự học tiếng Nhật - 1 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tháng.", DurationDays = 30, Price = 199000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = true, IsTrial = false, IsActive = true, SortOrder = 3, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 5, Name = "Tự học tiếng Nhật - 2 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tháng.", DurationDays = 60, Price = 349000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 2, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 6, Name = "Tự học tiếng Nhật - 3 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tháng.", DurationDays = 90, Price = 459000m, AiGradingQuota = 0, MeetingSessionCount = 0, IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) });
        }
    }
}
