using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class ContentAudioTranscriptionConfig : IEntityTypeConfiguration<ContentAudioTranscription>
{
    public void Configure(EntityTypeBuilder<ContentAudioTranscription> builder)
    {
        builder.ToTable("ContentAudioTranscriptions", table =>
        {
            table.HasCheckConstraint("CK_ContentAudioTranscription_Language", "\"LanguageCode\" = 'ja-JP'");
            table.HasCheckConstraint("CK_ContentAudioTranscription_Status",
                "\"Status\" IN ('Pending', 'Processing', 'Completed', 'Failed')");
            table.HasCheckConstraint("CK_ContentAudioTranscription_Attempts", "\"AttemptCount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AudioUrl).HasMaxLength(2048);
        builder.Property(x => x.AudioUrlHash).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.LanguageCode).HasMaxLength(10);
        builder.Property(x => x.RawTranscriptJson).HasColumnType("jsonb");
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(500);
        builder.HasIndex(x => x.AudioUrlHash).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.LeaseExpiresAtUtc });
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
