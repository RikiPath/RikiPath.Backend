using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorChatMessageConfig : IEntityTypeConfiguration<MentorChatMessage>
{
    public void Configure(EntityTypeBuilder<MentorChatMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MentorBookingId, x.Id });
        builder.HasIndex(x => new { x.MentorBookingId, x.ReadAt, x.SenderUserId });
    }
}
