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
            builder.Property(x => x.Code).HasMaxLength(30);
            builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
            builder.Property(x => x.Description).HasMaxLength(500);

            builder.HasData(
                new LanguageSkill
                {
                    Id = 1,
                    Name = "Vocabulary",
                    Code = "VOCABULARY",
                    Description = "Japanese vocabulary learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 2,
                    Name = "Kanji",
                    Code = "KANJI",
                    Description = "Japanese kanji learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 3,
                    Name = "Grammar",
                    Code = "GRAMMAR",
                    Description = "Japanese grammar learning skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 4,
                    Name = "Listening",
                    Code = "LISTENING",
                    Description = "Japanese listening comprehension skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new LanguageSkill
                {
                    Id = 5,
                    Name = "Reading",
                    Code = "READING",
                    Description = "Japanese reading comprehension skill.",
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                });
        }
    }
}
