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

            builder.HasData(
                new LanguageSkill
                {
                    Id = 1,
                    Name = "Vocabulary",
                    Description = "Japanese vocabulary learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 2,
                    Name = "Kanji",
                    Description = "Japanese kanji learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 3,
                    Name = "Grammar",
                    Description = "Japanese grammar learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 4,
                    Name = "Listening",
                    Description = "Japanese listening comprehension skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 5,
                    Name = "Reading",
                    Description = "Japanese reading comprehension skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                });
        }
    }
}
