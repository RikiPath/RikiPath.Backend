using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificationLevelSkillConfig : IEntityTypeConfiguration<CertificationLevelSkill>
    {
        public void Configure(EntityTypeBuilder<CertificationLevelSkill> builder)
        {
            builder.HasKey(x => new { x.CertificationLevelId, x.SkillId });

            builder.HasOne(x => x.CertificationLevel)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.CertificationLevelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Skill)
                .WithMany(x => x.CertificationLevelSkills)
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
