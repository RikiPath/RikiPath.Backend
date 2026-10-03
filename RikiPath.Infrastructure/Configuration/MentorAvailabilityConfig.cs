using RikiPath.Domain.Entities;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MentorAvailabilityConfig : IEntityTypeConfiguration<MentorAvailability>
    {
        public void Configure(EntityTypeBuilder<MentorAvailability> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.MentorId, x.StartTime, x.EndTime });

            // Mentor side (Cascade) and the 1-1 link to MentorBooking
            // (via MentorBooking.MentorAvailabilityId) are configured
            // in UserConfig / MentorBookingConfig respectively.

            // Fixed near-term demo slots for the seeded meeting Learner to try the FE booking flow.
            builder.HasData(
                new MentorAvailability
                {
                    Id = 990001, MentorId = 900002,
                    StartTime = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Unspecified),
                    IsBooked = true, IsApproved = true, AdminName = "Seed Demo", IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990002, MentorId = 900002,
                    StartTime = new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 3, 14, 0, 0, DateTimeKind.Unspecified),
                    IsBooked = false, IsApproved = true, AdminName = "Seed Demo", IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990003, MentorId = 900003,
                    StartTime = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Unspecified),
                    IsBooked = false, IsApproved = true, AdminName = "Seed Demo", IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                },
                new MentorAvailability
                {
                    Id = 990004, MentorId = 900003,
                    StartTime = new DateTime(2026, 10, 5, 13, 0, 0, DateTimeKind.Unspecified),
                    EndTime = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Unspecified),
                    IsBooked = false, IsApproved = true, AdminName = "Seed Demo", IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)
                });
        }
    }
}
