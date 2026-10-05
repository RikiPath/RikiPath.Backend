using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class JapaneseDictionaryEntryConfig : IEntityTypeConfiguration<JapaneseDictionaryEntry>
{
    public void Configure(EntityTypeBuilder<JapaneseDictionaryEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Surface).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ReadingKana).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ReadingRomaji).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Meaning).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.PartOfSpeech).HasMaxLength(100);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(30);

        builder.HasOne(x => x.UserAccount)
            .WithMany(x => x.PersonalDictionaryEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ReadingKana, x.UserId });
    }
}
