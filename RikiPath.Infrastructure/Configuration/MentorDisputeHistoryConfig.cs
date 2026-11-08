using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorDisputeHistoryConfig : IEntityTypeConfiguration<MentorDisputeHistory>
{
    public void Configure(EntityTypeBuilder<MentorDisputeHistory> b)
    {
        b.ToTable("MentorDisputeHistories");

        b.HasKey(x => x.Id);

        b.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(x => x.ActorRole)
            .IsRequired()
            .HasMaxLength(32);

        b.Property(x => x.Notes)
            .HasMaxLength(1000);

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.HasIndex(x => x.DisputeId)
            .HasDatabaseName("IX_MentorDisputeHistories_DisputeId");

        b.HasOne(x => x.Dispute)
            .WithMany(d => d.Histories)
            .HasForeignKey(x => x.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
