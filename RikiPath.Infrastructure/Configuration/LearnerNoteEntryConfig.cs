using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LearnerNoteEntryConfig : IEntityTypeConfiguration<LearnerNoteEntry>
    {
        public void Configure(EntityTypeBuilder<LearnerNoteEntry> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.LearnerNoteId).HasColumnName("VocabularyListId");
            builder.Property(x => x.VocabularyId).HasColumnName("VocabularyEntryId");
            builder.Property(x => x.KanjiId).HasColumnName("KanjiEntryId");
            builder.Property(x => x.ManualWord).HasMaxLength(200);
            builder.Property(x => x.ManualReading).HasMaxLength(200);
            builder.Property(x => x.ManualMeaning).HasMaxLength(500);

            builder.HasOne(x => x.LearnerNote)
                .WithMany(x => x.LearnerNoteEntries)
                .HasForeignKey(x => x.LearnerNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional links back to shared bank entries — do not delete a bank entry's
            // history just because a learner's note is removed, and vice versa.
            builder.HasOne(x => x.Vocabulary)
                .WithMany(x => x.LearnerNoteEntries)
                .HasForeignKey(x => x.VocabularyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Kanji)
                .WithMany(x => x.LearnerNoteEntries)
                .HasForeignKey(x => x.KanjiId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
