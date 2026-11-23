using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class LearnerHighlightConfig : IEntityTypeConfiguration<LearnerHighlight>
{
    public void Configure(EntityTypeBuilder<LearnerHighlight> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(40);
        builder.Property(x => x.TargetKey).HasMaxLength(200);
        builder.Property(x => x.SourcePath).HasMaxLength(300);
        builder.Property(x => x.SourceTitle).HasMaxLength(200);
        builder.Property(x => x.SelectedText).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Color).IsRequired().HasMaxLength(20);
        builder.Property(x => x.AnchorPrefix).HasMaxLength(200);
        builder.Property(x => x.AnchorSuffix).HasMaxLength(200);
        builder.Property(x => x.ContentVersionHash).HasMaxLength(128);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.TranslationSnapshot).HasMaxLength(1000);

        builder.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LinkedReviewCard)
            .WithMany()
            .HasForeignKey(x => x.LinkedReviewCardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.LinkedNoteEntry)
            .WithMany()
            .HasForeignKey(x => x.LinkedNoteEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.TargetType, x.TargetId });
        builder.HasIndex(x => new { x.UserId, x.TargetKey });
        builder.HasIndex(x => new { x.UserId, x.CreatedDate });
    }
}
