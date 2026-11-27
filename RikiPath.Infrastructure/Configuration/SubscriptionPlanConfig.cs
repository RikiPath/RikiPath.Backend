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
            builder.Property(x => x.PlanCategory).IsRequired().HasMaxLength(32).HasDefaultValue("self_study");
            builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SubscriptionPlans_Price", "\"Price\" >= 0");
                t.HasCheckConstraint("CK_SubscriptionPlans_DurationDays", "\"DurationDays\" >= 0");
                t.HasCheckConstraint("CK_SubscriptionPlans_AiGradingQuota", "\"AiGradingQuota\" >= 0");
                t.HasCheckConstraint("CK_SubscriptionPlans_SortOrder", "\"SortOrder\" >= 0");
            });
            builder.HasOne(x => x.CertificateLevel)
                .WithMany()
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);
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
                            new { FeaturesId = 1, SubscriptionPlansId = 6 }, new { FeaturesId = 2, SubscriptionPlansId = 6 }, new { FeaturesId = 3, SubscriptionPlansId = 6 }, new { FeaturesId = 4, SubscriptionPlansId = 6 }, new { FeaturesId = 5, SubscriptionPlansId = 6 }, new { FeaturesId = 6, SubscriptionPlansId = 6 },
                            new { FeaturesId = 7, SubscriptionPlansId = 7 }, new { FeaturesId = 8, SubscriptionPlansId = 7 },
                            // Credit plans used by the AI speaking/writing/grading/roadmap flows.
                            new { FeaturesId = 13, SubscriptionPlansId = 8101 },
                            new { FeaturesId = 14, SubscriptionPlansId = 8102 },
                            new { FeaturesId = 6, SubscriptionPlansId = 8103 },
                            new { FeaturesId = 12, SubscriptionPlansId = 8104 },
                            new { FeaturesId = 7, SubscriptionPlansId = 8105 }, new { FeaturesId = 8, SubscriptionPlansId = 8105 });
                    });

            builder.HasData(
                new SubscriptionPlan { Id = 1, Name = "Tự học tiếng Nhật - 1 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tuần.", DurationDays = 7, Price = 1000m, AiGradingQuota = 1, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 6, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 2, Name = "Tự học tiếng Nhật - 2 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tuần.", DurationDays = 14, Price = 1000m, AiGradingQuota = 2, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 5, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 3, Name = "Tự học tiếng Nhật - 3 tuần", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tuần.", DurationDays = 21, Price = 2000m, AiGradingQuota = 3, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 4, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 4, Name = "Tự học tiếng Nhật - 1 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tháng.", DurationDays = 30, Price = 2000m, AiGradingQuota = 5, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = true, IsTrial = false, IsActive = true, SortOrder = 3, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 5, Name = "Tự học tiếng Nhật - 2 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tháng.", DurationDays = 60, Price = 3000m, AiGradingQuota = 10, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 2, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 6, Name = "Tự học tiếng Nhật - 3 tháng", Description = "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tháng.", DurationDays = 90, Price = 3000m, AiGradingQuota = 15, MeetingSessionCount = 0, PlanCategory = "self_study", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 1, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 7, Name = "Meeting 1-1 với Mentor - 12 buổi", Description = "Gói demo gồm 12 buổi ôn luyện 1-1 với Mentor, thời hạn 90 ngày. Dữ liệu thanh toán demo được seed để kiểm thử đặt lịch trên frontend.", DurationDays = 90, Price = 1000m, AiGradingQuota = 0, MeetingSessionCount = 12, PlanCategory = "mentor_legacy", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 7, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) },

                // Small credit packages. They are intentionally separate from the
                // self-study packages so feature access can be audited by category.
                new SubscriptionPlan { Id = 8101, Name = "Luyện nói AI - 1 lượt", Description = "Một lượt luyện nói và chấm phát âm tiếng Nhật bằng AI.", DurationDays = 0, Price = 1000m, AiGradingQuota = 1, MeetingSessionCount = 0, PlanCategory = "speaking", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 10, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 8102, Name = "Luyện viết AI - 1 lượt", Description = "Một lượt chấm bài viết tiếng Nhật bằng AI.", DurationDays = 0, Price = 1000m, AiGradingQuota = 1, MeetingSessionCount = 0, PlanCategory = "writing", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 11, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 8103, Name = "Giải thích đáp án AI - 1 lượt", Description = "Một lượt giải thích chi tiết đáp án đề thi thử bằng AI.", DurationDays = 0, Price = 1000m, AiGradingQuota = 1, MeetingSessionCount = 0, PlanCategory = "ai_grading", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 12, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 8104, Name = "Lộ trình học AI - 1 lượt", Description = "Một lượt tạo lộ trình học cá nhân hóa bằng AI.", DurationDays = 0, Price = 1000m, AiGradingQuota = 1, MeetingSessionCount = 0, PlanCategory = "learning_path", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 13, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc) },
                new SubscriptionPlan { Id = 8105, Name = "Mentor - 1 buổi", Description = "Một buổi ôn luyện với mentor.", DurationDays = 30, Price = 1000m, AiGradingQuota = 0, MeetingSessionCount = 1, PlanCategory = "mentor_legacy", IsPopular = false, IsTrial = false, IsActive = true, SortOrder = 14, Revision = 1, IsDeleted = false, CreatedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc) });
        }
    }
}
