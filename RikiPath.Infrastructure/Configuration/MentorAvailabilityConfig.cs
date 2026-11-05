using RikiPath.Domain.Entities;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MentorAvailabilityConfig : IEntityTypeConfiguration<MentorAvailability>
    {
        private static readonly Guid Room1 = new("a1000000-0000-0000-0000-000000990001");
        private static readonly Guid Room2 = new("a1000000-0000-0000-0000-000000990002");
        private static readonly Guid Room3 = new("a1000000-0000-0000-0000-000000990003");
        private static readonly Guid Room4 = new("a1000000-0000-0000-0000-000000990004");

        public void Configure(EntityTypeBuilder<MentorAvailability> builder)
        {
            builder.HasKey(x => x.Id);
            // Khóa ghép cho reservation: Mentor của lượt giữ chỗ phải đúng Mentor sở hữu khung giờ.
            builder.HasAlternateKey(x => new { x.Id, x.MentorId });
            builder.HasIndex(x => new { x.MentorId, x.StartTime, x.EndTime });
            // Giữ đúng kiểu cột lịch sử; không tự chuyển giờ đã lưu sang timestamptz.
            builder.Property(x => x.StartTime).HasColumnType("timestamp without time zone");
            builder.Property(x => x.EndTime).HasColumnType("timestamp without time zone");
            // MentorAvailability là nguồn khung giờ có thể đặt; khóa sinh lịch chỉ áp dụng cho bản ghi mới.
            builder.HasOne(x => x.ScheduleRule).WithMany().HasForeignKey(x => x.MentorScheduleRuleId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.Property(x => x.GenerationKey).HasMaxLength(160);
            builder.Property(x => x.LocalBusinessDate).HasColumnType("date");
            builder.HasIndex(x => x.GenerationKey).IsUnique().HasFilter("\"GenerationKey\" IS NOT NULL");

            builder.Property(x => x.RoomId).IsRequired();
            builder.HasIndex(x => x.RoomId).IsUnique();

            builder.Property(x => x.MaxLearners).HasDefaultValue(MentorAvailability.DefaultMaxLearners);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.Property(x => x.Version).IsRowVersion();
            builder.Property(x => x.ChangeRequestReason).HasMaxLength(1000);
            builder.Property(x => x.ChangeResolution).HasMaxLength(1000);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MentorAvailability_BookedCount",
                    "\"BookedCount\" >= 0 AND \"BookedCount\" <= \"MaxLearners\"");
                // Khung giờ lịch sử không có metadata sinh lịch; khung giờ mới phải đủ dữ liệu và dài 90 phút.
                t.HasCheckConstraint("CK_MentorAvailability_GeneratedSlot",
                    "(\"GenerationKey\" IS NULL AND \"PolicyVersion\" IS NULL AND \"MentorScheduleRuleId\" IS NULL AND \"LocalBusinessDate\" IS NULL) OR " +
                    "(\"GenerationKey\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"PolicyVersion\" > 0 AND \"MentorScheduleRuleId\" IS NOT NULL AND " +
                    "\"LocalBusinessDate\" IS NOT NULL AND \"MaxLearners\" = 1 AND \"EndTime\" = \"StartTime\" + INTERVAL '90 minutes')");
            });

            builder.HasMany(x => x.MentorBookings)
                   .WithOne(b => b.MentorAvailability)
                   .HasForeignKey(b => b.MentorAvailabilityId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasData(
                new MentorAvailability
                {
                    Id = 990001,
                    MentorId = 900002,
                    RoomId = Room1,
                    MaxLearners = 4,
                    StartTime = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Unspecified),
                    BookedCount = 1,
                    IsApproved = true,
                    AdminName = "Seed Demo",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990002,
                    MentorId = 900002,
                    RoomId = Room2,
                    MaxLearners = 4,
                    StartTime = new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 3, 14, 0, 0, DateTimeKind.Unspecified),
                    BookedCount = 0,
                    IsApproved = true,
                    AdminName = "Seed Demo",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990003,
                    MentorId = 900003,
                    RoomId = Room3,
                    MaxLearners = 4,
                    StartTime = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Unspecified),
                    BookedCount = 0,
                    IsApproved = true,
                    AdminName = "Seed Demo",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990004,
                    MentorId = 900003,
                    RoomId = Room4,
                    MaxLearners = 4,
                    StartTime = new DateTime(2026, 10, 5, 13, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Unspecified),
                    BookedCount = 0,
                    IsApproved = true,
                    AdminName = "Seed Demo",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                });
        }
    }
}
