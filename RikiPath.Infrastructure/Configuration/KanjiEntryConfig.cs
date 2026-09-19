using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanjiEntryConfig : IEntityTypeConfiguration<KanjiEntry>
    {
        public void Configure(EntityTypeBuilder<KanjiEntry> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Character).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ReviewedByName).HasMaxLength(200);

            builder.HasOne(x => x.CertificationLevel)
                .WithMany(x => x.KanjiEntries)
                .HasForeignKey(x => x.CertificationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ContentAuthor)
                .WithMany(x => x.AuthoredKanjiEntries)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificationLevelId, x.Status });
        }
    }
}
