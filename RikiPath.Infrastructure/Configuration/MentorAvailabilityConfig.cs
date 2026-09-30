using RikiPath.Domain.Entities;
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
        }
    }
}
