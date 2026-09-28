using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class NoteConfig : IEntityTypeConfiguration<Note>
    {
        public void Configure(EntityTypeBuilder<Note> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.MentorBooking)
                .WithOne(x => x.Note)
                .HasForeignKey<Note>(x => x.MentorBookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mentor side (Restrict) configured in UserConfig.
        }
    }
}
