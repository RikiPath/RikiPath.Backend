using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class ContentPassageConfig : IEntityTypeConfiguration<ContentPassage>
{
    public void Configure(EntityTypeBuilder<ContentPassage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PassageCode).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Topic).HasMaxLength(200);
        builder.Property(x => x.Instructions).HasMaxLength(1000);
        builder.Property(x => x.ListeningSection).HasMaxLength(120);
        builder.Property(x => x.AudioTranscript).HasColumnType("text");
        builder.Property(x => x.PlaybackPolicyJson).HasColumnType("jsonb");
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(40).HasDefaultValue("Manual");
        builder.Property(x => x.SourceReference).HasMaxLength(300);
        builder.Property(x => x.RightsStatus).HasMaxLength(100);
        builder.Property(x => x.AiProvenanceJson).HasColumnType("jsonb");
        builder.Property(x => x.ContentFingerprint).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ReviewHistoryJson).HasColumnType("jsonb");

        builder.HasOne(x => x.LanguageSkill)
            .WithMany()
            .HasForeignKey(x => x.LanguageSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CertificateLevel)
            .WithMany()
            .HasForeignKey(x => x.CertificateLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PassageIdentity);
        builder.HasIndex(x => x.ContentFingerprint);
        builder.HasIndex(x => new { x.CertificateLevelId, x.LanguageSkillId, x.PublishStatus });
    }
}
