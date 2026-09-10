using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeTestSectionConfig : IEntityTypeConfiguration<PracticeTestSection>
    {
        public void Configure(EntityTypeBuilder<PracticeTestSection> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(200);

            builder.HasOne(x => x.PracticeTest)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.PracticeTestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Skill)
                .WithMany(x => x.PracticeTestSections)
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
