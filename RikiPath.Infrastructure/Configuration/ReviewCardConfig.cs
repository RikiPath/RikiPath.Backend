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
            builder.HasIndex(x => new { x.UserId, x.NextReviewDate });
            builder.Property(x => x.WritingPracticeMode).HasMaxLength(20);
            builder.HasIndex(x => new { x.UserId, x.KanjiId, x.Mode }).IsUnique();
        }
    }
}
