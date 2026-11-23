using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ReviewCardConfig : IEntityTypeConfiguration<ReviewCard>
    {
        public void Configure(EntityTypeBuilder<ReviewCard> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.LearnerNoteEntryId).HasColumnName("VocabularyNoteEntryId");
            builder.Property(x => x.KanjiId).HasColumnName("KanjiEntryId");
            builder.Property(x => x.GrammarPatternId).HasColumnName("GrammarPointId");

            builder.HasOne(x => x.LearnerNoteEntry)
                .WithMany(x => x.ReviewCards)
                .HasForeignKey(x => x.LearnerNoteEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Kanji)
                .WithMany(x => x.ReviewCards)
                .HasForeignKey(x => x.KanjiId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.GrammarPattern)
                .WithMany(x => x.ReviewCards)
                .HasForeignKey(x => x.GrammarPatternId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserAccount side (Cascade) configured in UserConfig.
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RikiPath.Domain.Enums.SrsCardStatus.Active).HasSentinel((RikiPath.Domain.Enums.SrsCardStatus)(-1));
            builder.Property(x => x.LearningState).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RikiPath.Domain.Enums.SrsLearningState.New).HasSentinel((RikiPath.Domain.Enums.SrsLearningState)(-1));
            builder.Property(x => x.CardType).IsRequired().HasMaxLength(30).HasDefaultValue("Vocabulary");
            builder.Property(x => x.SourceType).IsRequired().HasMaxLength(40).HasDefaultValue("Manual");
            builder.Property(x => x.SourceVersion).HasDefaultValue(1);
            builder.Property(x => x.SourceLabel).HasMaxLength(300);
            builder.Property(x => x.NormalizedContentKey).IsRequired().HasMaxLength(300).HasDefaultValue(string.Empty);
            builder.Property(x => x.SourceLinksJson).HasMaxLength(4000);
            builder.Property(x => x.FrontText).IsRequired().HasMaxLength(500).HasDefaultValue(string.Empty);
            builder.Property(x => x.BackText).IsRequired().HasMaxLength(1000).HasDefaultValue(string.Empty);
            builder.Property(x => x.Reading).HasMaxLength(300);
            builder.Property(x => x.Meaning).HasMaxLength(500);
            builder.Property(x => x.ExampleSentence).HasMaxLength(1000);
            builder.Property(x => x.ExampleMeaning).HasMaxLength(1000);
            builder.Property(x => x.ContextSnippet).HasMaxLength(1000);
            builder.Property(x => x.JlptLevel).HasMaxLength(10);
            builder.Property(x => x.AudioUrl).HasMaxLength(1000);
            builder.Property(x => x.PersonalNote).HasMaxLength(1000);
            builder.Property(x => x.AcceptedReadingsJson).HasMaxLength(2000);
            builder.Property(x => x.SynonymsJson).HasMaxLength(2000);
            builder.Property(x => x.Revision).HasDefaultValue(1);
            builder.Property(x => x.WritingPracticeMode).HasMaxLength(20);

            builder.HasOne(x => x.Vocabulary)
                .WithMany()
                .HasForeignKey(x => x.VocabularyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Lesson)
                .WithMany()
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.SrsCardTemplate)
                .WithMany()
                .HasForeignKey(x => x.SrsCardTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => new { x.UserId, x.NextReviewDate });
            builder.HasIndex(x => new { x.UserId, x.Status, x.LearningState, x.NextReviewDate });
            builder.HasIndex(x => new { x.UserId, x.KanjiId, x.Mode })
                .IsUnique()
                .HasFilter("\"KanjiEntryId\" IS NOT NULL");
            builder.HasIndex(x => new { x.UserId, x.VocabularyId, x.Mode })
                .IsUnique()
                .HasFilter("\"VocabularyId\" IS NOT NULL");
            builder.HasIndex(x => new { x.UserId, x.GrammarPatternId, x.Mode })
                .IsUnique()
                .HasFilter("\"GrammarPointId\" IS NOT NULL");
            builder.HasIndex(x => new { x.UserId, x.NormalizedContentKey, x.Mode })
                .IsUnique()
                .HasFilter("\"NormalizedContentKey\" <> '' AND \"IsDeleted\" = false");
        }
    }
}
