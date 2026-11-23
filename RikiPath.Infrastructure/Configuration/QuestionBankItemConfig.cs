using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class QuestionBankItemConfig : IEntityTypeConfiguration<QuestionBankItem>
{
    public void Configure(EntityTypeBuilder<QuestionBankItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionGroupTitle).HasMaxLength(200);
        builder.Property(x => x.TemplateKind).IsRequired().HasMaxLength(60).HasDefaultValue("VocabMeaning");
        builder.Property(x => x.QuestionType).IsRequired().HasMaxLength(40).HasDefaultValue("SingleChoice");
        builder.Property(x => x.QuestionText).IsRequired().HasColumnType("text");
        builder.Property(x => x.SkillMetadataJson).HasColumnType("jsonb");
        builder.Property(x => x.InteractionDataJson).HasColumnType("jsonb");
        builder.Property(x => x.AnswerKeyJson).HasColumnType("jsonb");
        builder.Property(x => x.OptionsJson).HasColumnType("jsonb");
        builder.Property(x => x.AudioTranscript).HasColumnType("text");
        builder.Property(x => x.PlaybackPolicyJson).HasColumnType("jsonb");
        builder.Property(x => x.TagsJson).HasColumnType("jsonb");
        builder.Property(x => x.Difficulty).HasMaxLength(30);
        builder.Property(x => x.Topic).HasMaxLength(150);
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(40).HasDefaultValue("Manual");
        builder.Property(x => x.SourceReference).HasMaxLength(300);
        builder.Property(x => x.RightsStatus).HasMaxLength(100);
        builder.Property(x => x.AiProvenanceJson).HasColumnType("jsonb");
        builder.Property(x => x.ContentFingerprint).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ReviewHistoryJson).HasColumnType("jsonb");

        builder.HasOne(x => x.CertificateLevel)
            .WithMany()
            .HasForeignKey(x => x.CertificateLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LanguageSkill)
            .WithMany()
            .HasForeignKey(x => x.LanguageSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Passage)
            .WithMany(p => p.Questions)
            .HasForeignKey(x => x.PassageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.QuestionIdentity);
        builder.HasIndex(x => x.ContentFingerprint);
        builder.HasIndex(x => new { x.CertificateLevelId, x.LanguageSkillId, x.PublishStatus, x.IsArchivedVersion });
        builder.HasIndex(x => x.AuthorUserId);
    }
}
