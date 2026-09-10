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
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.VocabularyEntries)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContentAuthor / ReviewedBy relationships configured from UserConfig.
            builder.HasIndex(x => new { x.Word, x.JlptLevelId });
        }
    }
}
