using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class VocabularyNoteEntryConfig : IEntityTypeConfiguration<VocabularyNoteEntry>
    {
        public void Configure(EntityTypeBuilder<VocabularyNoteEntry> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ManualWord).HasMaxLength(200);
            builder.Property(x => x.ManualReading).HasMaxLength(200);
            builder.Property(x => x.ManualMeaning).HasMaxLength(500);

            builder.HasOne(x => x.VocabularyList)
                .WithMany(x => x.VocabularyNoteEntries)
                .HasForeignKey(x => x.VocabularyListId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional links back to shared bank entries — do not delete a bank entry's
            // history just because a learner's note is removed, and vice versa.
            builder.HasOne(x => x.VocabularyEntry)
                .WithMany(x => x.VocabularyNoteEntries)
                .HasForeignKey(x => x.VocabularyEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.KanjiEntry)
                .WithMany(x => x.VocabularyNoteEntries)
                .HasForeignKey(x => x.KanjiEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
