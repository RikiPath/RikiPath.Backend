using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestConfig : IEntityTypeConfiguration<MockTest>
    {
        public void Configure(EntityTypeBuilder<MockTest> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.IncludedSkillsJson).HasColumnType("jsonb");
            builder.Property(x => x.TagsJson).HasColumnType("jsonb");
            builder.Property(x => x.SourceType).HasMaxLength(40);
            builder.Property(x => x.SourceReference).HasMaxLength(300);
            builder.Property(x => x.RightsStatus).HasMaxLength(100);
            builder.Property(x => x.BlueprintPolicyJson).HasColumnType("jsonb");
            builder.Property(x => x.AiProvenanceJson).HasColumnType("jsonb");
            builder.Property(x => x.ReviewHistoryJson).HasColumnType("jsonb");

            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.MockTests)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateLevelId });
        }
    }
}
