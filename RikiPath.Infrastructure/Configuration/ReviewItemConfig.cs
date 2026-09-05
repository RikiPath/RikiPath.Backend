using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ReviewItemConfig : IEntityTypeConfiguration<ReviewItem>
    {
        public void Configure(EntityTypeBuilder<ReviewItem> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.VocabularyNoteEntry)
                .WithMany(x => x.ReviewItems)
                .HasForeignKey(x => x.VocabularyNoteEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.KanjiEntry)
                .WithMany(x => x.ReviewItems)
                .HasForeignKey(x => x.KanjiEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.GrammarPoint)
                .WithMany(x => x.ReviewItems)
                .HasForeignKey(x => x.GrammarPointId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasIndex(x => new { x.UserId, x.NextReviewDate });
        }
    }
}
