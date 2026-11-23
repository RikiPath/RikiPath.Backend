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
            builder.Property(x => x.ItemKind).IsRequired().HasMaxLength(32).HasDefaultValue("manual");
            builder.Property(x => x.HighlightColor).HasMaxLength(24);
            builder.Property(x => x.SourceType).HasMaxLength(40);
            builder.Property(x => x.SourcePath).HasMaxLength(300);
            builder.Property(x => x.SourceTitle).HasMaxLength(200);
            builder.Property(x => x.ContextSnippet).HasMaxLength(600);
            builder.Property(x => x.ExampleSentence).HasMaxLength(500);
            builder.Property(x => x.ExampleMeaning).HasMaxLength(500);
            builder.Property(x => x.TagsJson).IsRequired().HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            builder.Property(x => x.IsFavorite).HasDefaultValue(false);
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();

            builder.HasOne(x => x.LearnerNote)
                .WithMany(x => x.LearnerNoteEntries)
                .HasForeignKey(x => x.LearnerNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.NotebookPage)
                .WithMany(x => x.SavedItems)
                .HasForeignKey(x => x.NotebookPageId)
                .OnDelete(DeleteBehavior.Restrict);

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

            builder.HasOne(x => x.GrammarPattern)
                .WithMany()
                .HasForeignKey(x => x.GrammarPatternId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.NotebookPageId, x.IsDeleted, x.SortOrder });
            builder.HasIndex(x => new { x.LearnerNoteId, x.IsDeleted });
        }
    }
}
