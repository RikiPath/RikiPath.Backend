using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration;

public class SrsCardTemplateConfig : IEntityTypeConfiguration<SrsCardTemplate>
{
    public void Configure(EntityTypeBuilder<SrsCardTemplate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CardType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.FrontText).IsRequired().HasMaxLength(500);
        builder.Property(x => x.BackText).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Reading).HasMaxLength(300);
        builder.Property(x => x.Meaning).HasMaxLength(500);
        builder.Property(x => x.ExampleSentence).HasMaxLength(1000);
        builder.Property(x => x.ExampleMeaning).HasMaxLength(1000);
        builder.Property(x => x.UsageNotes).HasMaxLength(2000);
        builder.Property(x => x.AcceptedReadingsJson).HasMaxLength(2000);
        builder.Property(x => x.SynonymsJson).HasMaxLength(2000);
        builder.Property(x => x.AudioUrl).HasMaxLength(1000);
        builder.Property(x => x.JlptLevel).HasMaxLength(10);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.UnpublishedReason).HasMaxLength(500);
        builder.Property(x => x.NormalizedContentKey).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.Version).HasDefaultValue(1);

        builder.HasOne(x => x.CertificateLevel)
            .WithMany()
            .HasForeignKey(x => x.CertificateLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Lesson)
            .WithMany()
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Vocabulary)
            .WithMany()
            .HasForeignKey(x => x.VocabularyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Kanji)
            .WithMany()
            .HasForeignKey(x => x.KanjiId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.GrammarPattern)
            .WithMany()
            .HasForeignKey(x => x.GrammarPatternId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SubmittedByUser)
            .WithMany()
            .HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Status, x.JlptLevel, x.CardType });
        builder.HasIndex(x => new { x.SubmittedByUserId, x.Status });
        builder.HasIndex(x => x.NormalizedContentKey);
    }
}
