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
            builder.HasIndex(x => new { x.MentorId, x.StartTime, x.EndTime });

            builder.Property(x => x.RoomId).IsRequired();
            builder.HasIndex(x => x.RoomId).IsUnique();

            builder.Property(x => x.MaxLearners).HasDefaultValue(MentorAvailability.DefaultMaxLearners);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_MentorAvailability_BookedCount",
                "[BookedCount] >= 0 AND [BookedCount] <= [MaxLearners]"));

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