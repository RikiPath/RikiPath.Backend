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
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.KanjiEntries)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContentAuthor / ReviewedBy relationships configured from UserConfig.
            builder.HasIndex(x => new { x.Character, x.JlptLevelId });
        }
    }
}
