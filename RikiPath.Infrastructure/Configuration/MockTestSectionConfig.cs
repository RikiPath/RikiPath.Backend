using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestSectionConfig : IEntityTypeConfiguration<MockTestSection>
    {
        public void Configure(EntityTypeBuilder<MockTestSection> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockTestId).HasColumnName("PracticeTestId");
            builder.Property(x => x.LanguageSkillId).HasColumnName("SkillId");
            builder.Property(x => x.Title).IsRequired().HasMaxLength(200);

            builder.HasOne(x => x.MockTest)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.MockTestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.LanguageSkill)
                .WithMany(x => x.MockTestSections)
                .HasForeignKey(x => x.LanguageSkillId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
