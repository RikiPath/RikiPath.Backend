using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LanguageSkillConfig : IEntityTypeConfiguration<LanguageSkill>
    {
        public void Configure(EntityTypeBuilder<LanguageSkill> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
            builder.HasIndex(x => x.Name).IsUnique();
            builder.Property(x => x.Description).HasMaxLength(500);
        }
    }
}
