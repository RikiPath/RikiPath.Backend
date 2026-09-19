using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class VocabularyEntryConfig : IEntityTypeConfiguration<VocabularyEntry>
    {
        public void Configure(EntityTypeBuilder<VocabularyEntry> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Word).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Reading).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ReviewedByName).HasMaxLength(200);

            builder.HasOne(x => x.CertificationLevel)
                .WithMany(x => x.VocabularyEntries)
                .HasForeignKey(x => x.CertificationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ContentAuthor)
                .WithMany(x => x.AuthoredVocabularyEntries)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificationLevelId, x.Status });
        }
    }
}
