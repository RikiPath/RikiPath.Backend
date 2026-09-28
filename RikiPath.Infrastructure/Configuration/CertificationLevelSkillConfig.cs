using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificationLevelSkillConfig : IEntityTypeConfiguration<CertificationLevelSkill>
    {
        public void Configure(EntityTypeBuilder<CertificationLevelSkill> builder)
        {
            builder.HasKey(x => new { x.CertificateLevelId, x.LanguageSkillId });
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");
            builder.Property(x => x.LanguageSkillId).HasColumnName("SkillId");

            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.LanguageSkill)
                .WithMany(x => x.CertificationLevelSkills)
                .HasForeignKey(x => x.LanguageSkillId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
